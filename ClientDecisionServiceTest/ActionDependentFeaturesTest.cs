﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ActionDependentFeaturesTest : MockCommandTestBase
    {
        [TestMethod]
        public void TestADFExplorationResult()
        {
            joinServer.Reset();

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue,
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress
            };

            using (var ds = DecisionService
                .WithRanker(dsConfig)
                .With<TestADFContext>()
                .WithTopSlotEpsilonGreedy(.5f)
                .ExploitUntilModelReady(new TestADFPolicy()))
            {
                string uniqueKey = "eventid";

                for (int i = 1; i <= 100; i++)
                {
                    var adfContext = new TestADFContext(i);
                    int[] action = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, adfContext);

                    Assert.AreEqual(i, action.Length);

                    // verify all unique actions in the list
                    Assert.AreEqual(action.Length, action.Distinct().Count());

                    // verify the actions are in the expected range
                    Assert.AreEqual((i * (i + 1)) / 2, action.Sum(a => a));

                    ds.ReportReward(i / 100f, new UniqueEventID { Key = uniqueKey });
                }
            }
            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        public void TestADFModelUpdateFromStream()
        {
            joinServer.Reset();

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            using (var ds = DecisionService
                .WithRanker(dsConfig)
                .With<TestADFContextWithFeatures, TestADFFeatures>(context => context.ActionDependentFeatures)
                .WithTopSlotEpsilonGreedy(.5f)
                .ExploitUntilModelReady(new TestADFWithFeaturesPolicy()))
            {
                string uniqueKey = "eventid";

                for (int i = 1; i <= 100; i++)
                {
                    Random rg = new Random(i);

                    if (i % 50 == 1)
                    {
                        int modelIndex = i / 50;
                        byte[] modelContent = commandCenter.GetCBADFModelBlobContent(numExamples: 3 + modelIndex, numFeatureVectors: 4 + modelIndex);
                        using (var modelStream = new MemoryStream(modelContent))
                        {
                            ds.UpdateModel(modelStream);
                        }
                    }

                    int numActions = rg.Next(5, 20);
                    var context = TestADFContextWithFeatures.CreateRandom(numActions, rg);

                    int[] action = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, context);

                    Assert.AreEqual(numActions, action.Length);

                    // verify all unique actions in the list
                    Assert.AreEqual(action.Length, action.Distinct().Count());

                    // verify the actions are in the expected range
                    Assert.AreEqual((numActions * (numActions + 1)) / 2, action.Sum(a => a));

                    ds.ReportReward(i / 100f, new UniqueEventID { Key = uniqueKey });
                }

                ds.Flush();
            }
            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));
        }
    }
}