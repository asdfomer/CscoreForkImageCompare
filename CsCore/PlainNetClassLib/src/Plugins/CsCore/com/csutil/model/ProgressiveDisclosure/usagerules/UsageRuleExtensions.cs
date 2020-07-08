using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using com.csutil.keyvaluestore;
using com.csutil.logging.analytics;

namespace com.csutil.model.usagerules {

    public static class UsageRuleExtensions {

        public static UsageRule SetupUsing(this UsageRule self, LocalAnalytics analytics) {
            self.isTrue = async () => {
                switch (self.ruleType) {

                    case UsageRule.AppUsedXDays: return await self.IsAppUsedXDays(analytics);
                    case UsageRule.AppUsedInTheLastXDays: return self.IsAppUsedInTheLastXDays();

                    case UsageRule.AppNotUsedXDays: return !await self.IsAppUsedXDays(analytics);
                    case UsageRule.AppNotUsedInTheLastXDays: return !self.IsAppUsedInTheLastXDays();

                    case UsageRule.FeatureUsedInTheLastXDays: return await self.IsFeatureUsedInTheLastXDays(analytics);
                    case UsageRule.FeatureUsedXDays: return await self.IsFeatureUsedXDays(analytics);
                    case UsageRule.FeatureUsedXTimes: return await self.IsFeatureUsedXTimes(analytics);

                    case UsageRule.FeatureNotUsedInTheLastXDays: return !await self.IsFeatureUsedInTheLastXDays(analytics);
                    case UsageRule.FeatureNotUsedXDays: return !await self.IsFeatureUsedXDays(analytics);
                    case UsageRule.FeatureNotUsedXTimes: return !await self.IsFeatureUsedXTimes(analytics);

                    case UsageRule.ConcatRule:
                        foreach (var rule in self.andRules) {
                            if (rule.isTrue == null) { rule.SetupUsing(analytics); }
                            if (!await rule.isTrue()) { return false; }
                        }
                        return true;

                    default:
                        Log.e("Unknown ruleType: " + self.ruleType);
                        return false;
                }
            };
            return self;
        }

        public static bool IsAppUsedInTheLastXDays(this UsageRule self) {
            var tSinceLatestLaunch = DateTimeV2.UtcNow - EnvironmentV2.instance.systemInfo.GetLatestLaunchDate();
            return tSinceLatestLaunch.Days < self.days;
        }

        public static async Task<bool> IsFeatureUsedInTheLastXDays(this UsageRule self, LocalAnalytics analytics) {
            var allFeatureEvents = await analytics.GetAllFeatureEvents(self.featureId);
            if (allFeatureEvents.IsNullOrEmpty()) { return false; }
            DateTime lastEvent = allFeatureEvents.Last().GetDateTimeUtc();
            TimeSpan lastEventVsNow = DateTimeV2.UtcNow - lastEvent;
            return lastEventVsNow.Days <= self.days;
        }

        public static async Task<bool> IsFeatureUsedXTimes(this UsageRule self, LocalAnalytics analytics) {
            var allFeatureEvents = await analytics.GetAllFeatureEvents(self.featureId);
            var startEvents = allFeatureEvents.Filter(x => x.action == EventConsts.START);
            return startEvents.Count() >= self.timesUsed.Value;
        }

        public static IEnumerable<IGrouping<DateTime, AppFlowEvent>> GroupByDay(this IEnumerable<AppFlowEvent> self) {
            return self.GroupBy(x => x.GetDateTimeUtc().Date, x => x);
        }

        public static async Task<bool> IsAppUsedXDays(this UsageRule self, LocalAnalytics analytics) {
            var allEvents = await analytics.GetAll();
            return allEvents.GroupByDay().Count() >= self.days;
        }

        public static async Task<bool> IsFeatureUsedXDays(this UsageRule self, LocalAnalytics analytics) {
            var allFeatureEvents = await analytics.GetAllFeatureEvents(self.featureId);
            return allFeatureEvents.GroupByDay().Count() >= self.days;
        }

        private static async Task<IEnumerable<AppFlowEvent>> GetAllFeatureEvents(this LocalAnalytics self, string featureId) {
            if (!self.categoryStores.ContainsKey(featureId)) { return Enumerable.Empty<AppFlowEvent>(); }
            return await self.categoryStores[featureId].GetAll();
        }

        public static async Task<IEnumerable<UsageRule>> GetRulesInitialized(this KeyValueStoreTypeAdapter<UsageRule> self, LocalAnalytics analytics) {
            var rules = await self.GetAll();
            foreach (var rule in rules) {
                if (!rule.concatRuleIds.IsNullOrEmpty()) {
                    rule.andRules = new List<UsageRule>();
                    foreach (var id in rule.concatRuleIds) {
                        rule.andRules.Add(await self.Get(id, null));
                    }
                }
                if (rule.isTrue == null) { rule.SetupUsing(analytics); }
            }
            return rules;
        }

    }

}