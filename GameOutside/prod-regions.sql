ALTER TABLE "ActivityCoopBossInfoSet" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "ActivityCoopBossInfoSet" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityCoopBossInfoSet" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityCoopBossInfoSet"@"PK_ActivityCoopBossInfoSet" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "ActivityEndlessChallenges" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "ActivityEndlessChallenges" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityEndlessChallenges" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityEndlessChallenges"@"PK_ActivityEndlessChallenges" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "ActivityLuckyStars" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "ActivityLuckyStars" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityLuckyStars" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityLuckyStars"@"PK_ActivityLuckyStars" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "ActivityPiggyBanks" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "ActivityPiggyBanks" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityPiggyBanks" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityPiggyBanks"@"PK_ActivityPiggyBanks" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "ActivityUnrivaledGods" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "ActivityUnrivaledGods" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityUnrivaledGods" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityUnrivaledGods"@"PK_ActivityUnrivaledGods" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "PaidOrders" ADD COLUMN crdb_region crdb_internal_region NOT NULL DEFAULT default_to_database_primary_region(gateway_region())::crdb_internal_region;
ALTER TABLE "PaidOrders" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "PaidOrders" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "PaidOrders"@"PK_PaidOrders" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "PaidOrders"@"IX_PaidOrders_PlayerId_ClaimStatus" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "PlatformNotifies" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "PlatformNotifies" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "PlatformNotifies" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "PlatformNotifies"@"IX_PlatformNotifies_PlayerId_ClaimStatus_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "PlatformNotifies"@"PK_PlatformNotifies" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "PromotionStatus" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "PromotionStatus" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "PromotionStatus" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "PromotionStatus"@"PK_PromotionStatus" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "RedisLuaScripts" SET LOCALITY GLOBAL;
ALTER TABLE "SeasonRefreshedHistories" SET LOCALITY GLOBAL;
ALTER TABLE "ServerDataset" SET LOCALITY GLOBAL;
ALTER TABLE "SocInfos" SET LOCALITY GLOBAL;


ALTER TABLE "UserAchievements" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserAchievements" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserAchievements" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserAchievements"@"PK_UserAchievements" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserAssets" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserAssets" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserAssets" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserAssets"@"PK_UserAssets" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserAttendances" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserAttendances" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserAttendances" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserAttendances"@"PK_UserAttendances" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserBattlePassInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserBattlePassInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserBattlePassInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserBattlePassInfos"@"PK_UserBattlePassInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserBeginnerTasks" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserBeginnerTasks" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserBeginnerTasks" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserBeginnerTasks"@"PK_UserBeginnerTasks" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserCommodityBoughtRecords" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserCommodityBoughtRecords" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserCommodityBoughtRecords" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserCommodityBoughtRecords"@"PK_UserCommodityBoughtRecords" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserCustomCardPools" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserCustomCardPools" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserCustomCardPools" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserCustomCardPools"@"PK_UserCustomCardPools" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserCustomData" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserCustomData" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserCustomData" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserCustomData"@"PK_UserCustomData" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserDailyStoreIndices" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserDailyStoreIndices" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserDailyStoreIndices" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserDailyStoreIndices"@"PK_UserDailyStoreIndices" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserDailyStoreItems" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserDailyStoreItems" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserDailyStoreItems" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserDailyStoreItems"@"PK_UserDailyStoreItems" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserDailyTasks" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserDailyTasks" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserDailyTasks" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserDailyTasks"@"PK_UserDailyTasks" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserDailyTreasureBoxProgresses" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserDailyTreasureBoxProgresses" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserDailyTreasureBoxProgresses" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserDailyTreasureBoxProgresses"@"PK_UserDailyTreasureBoxProgresses" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserDivisions" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserDivisions" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserDivisions" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserDivisions"@"PK_UserDivisions" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserEndlessRanks" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserEndlessRanks" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserEndlessRanks" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserEndlessRanks"@"PK_UserEndlessRanks" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserEndlessRanks"@"IX_UserEndlessRanks_SurvivorScore_SurvivorTimestamp" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserEndlessRanks"@"IX_UserEndlessRanks_TowerDefenceScore_TowerDefenceTimestamp" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserEndlessRanks"@"IX_UserEndlessRanks_TrueEndlessScore_TrueEndlessTimestamp" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserFixedLevelMapProgress" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserFixedLevelMapProgress" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserFixedLevelMapProgress" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserFixedLevelMapProgress"@"PK_UserFixedLevelMapProgress" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserFortuneBagInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserFortuneBagInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserFortuneBagInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserFortuneBagInfos"@"PK_UserFortuneBagInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserGameInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserGameInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserGameInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserGameInfos"@"PK_UserGameInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


-- TODO: UserGlobalInfos 暂时不配置

ALTER TABLE "UserIapPurchases" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserIapPurchases" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserIapPurchases" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserIapPurchases"@"PK_UserIapPurchases" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserIapPurchases"@"IX_UserIapPurchases_PlayerId_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserIdleRewardInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserIdleRewardInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserIdleRewardInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserIdleRewardInfos"@"PK_UserIdleRewardInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserInfos"@"PK_UserInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserMallAdvertisements" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserMallAdvertisements" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserMallAdvertisements" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserMallAdvertisements"@"PK_UserMallAdvertisements" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserMonthPassInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserMonthPassInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserMonthPassInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserMonthPassInfos"@"PK_UserMonthPassInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserRanks" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserRanks" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserRanks" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserRanks"@"PK_UserRanks" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserRanks"@"IX_UserRanks_HighestScore_Timestamp" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserRanks"@"IX_UserRanks_SeasonNumber_Division_GroupId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserStarStoreStatus" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserStarStoreStatus" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserStarStoreStatus" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserStarStoreStatus"@"PK_UserStarStoreStatus" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserCards" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserCards" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserCards" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserCards"@"PK_UserCards" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserCards"@"IX_UserCards_PlayerId_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserItems" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserItems" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserItems" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserItems"@"PK_UserItems" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserItems"@"IX_UserItems_PlayerId_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserTreasureBoxes" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserTreasureBoxes" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserTreasureBoxes" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserTreasureBoxes"@"PK_UserTreasureBoxes" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserTreasureBoxes"@"IX_UserTreasureBoxes_PlayerId_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;


ALTER TABLE "UserHistories" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserHistories" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserHistories" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserHistories"@"PK_UserHistories" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserHistories"@"IX_UserHistories_PlayerId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserHistories"@"IX_UserHistories_PlayerId_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "UserEncryptionInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserEncryptionInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserEncryptionInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserEncryptionInfos"@"PK_UserEncryptionInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivityTreasureMazeInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "ActivityTreasureMazeInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityTreasureMazeInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityTreasureMazeInfos"@"PK_ActivityTreasureMazeInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "UserH5FriendActivityInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "UserH5FriendActivityInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "UserH5FriendActivityInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "UserH5FriendActivityInfos"@"PK_UserH5FriendActivityInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "InvitationCodeClaimRecords" SET LOCALITY GLOBAL;

ALTER TABLE "IosGameCenterRewardInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
    END
) STORED;
ALTER TABLE "IosGameCenterRewardInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "IosGameCenterRewardInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "IosGameCenterRewardInfos"@"PK_IosGameCenterRewardInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivitySlotMachines" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "ActivitySlotMachines" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivitySlotMachines" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivitySlotMachines"@"PK_ActivitySlotMachines" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivityOneShotKills" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "ActivityOneShotKills" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityOneShotKills" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityOneShotKills"@"PK_ActivityOneShotKills" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "PaidOrderWithShards" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "PaidOrderWithShards" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "PaidOrderWithShards" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "PaidOrderWithShards"@"PK_PaidOrderWithShards" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "PaidOrderWithShards"@"IX_PaidOrderWithShards_PlayerId_ShardId_ClaimStatus" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "LocalRedisLuaScripts" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "LocalRedisLuaScripts" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "LocalRedisLuaScripts" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "LocalRedisLuaScripts"@"PK_LocalRedisLuaScripts" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER PARTITION "eu-fr-aws" OF INDEX "UserRanks"@"IX_UserRanks_SeasonNumber_Division_GroupId_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserRanks"@"IX_UserRanks_SeasonNumber_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;
ALTER PARTITION "eu-fr-aws" OF INDEX "UserEndlessRanks"@"IX_UserEndlessRanks_SeasonNumber_ShardId" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivityTreasureHunts" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "ActivityTreasureHunts" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityTreasureHunts" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityTreasureHunts"@"PK_ActivityTreasureHunts" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivityRpgGames" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "ActivityRpgGames" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityRpgGames" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityRpgGames"@"PK_ActivityRpgGames" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "PlayerPunishmentTasks" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "PlayerPunishmentTasks" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "PlayerPunishmentTasks" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "PlayerPunishmentTasks"@"PK_PlayerPunishmentTasks" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivityCsgoStyleLotteryInfos" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "ActivityCsgoStyleLotteryInfos" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityCsgoStyleLotteryInfos" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityCsgoStyleLotteryInfos"@"PK_ActivityCsgoStyleLotteryInfos" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;

ALTER TABLE "ActivityLoogGames" ADD COLUMN crdb_region crdb_internal_region NOT NULL AS (
    CASE
        WHEN "ShardId" IN (1000) THEN 'eu-fr-aws'
        WHEN "ShardId" IN (2000) THEN 'cn-hangzhou-aliyun'
        WHEN "ShardId" IN (3000) THEN 'ap-sg-aws'
        WHEN "ShardId" IN (5000) THEN 'us-siliconvalley-aliyun'
        END
    ) STORED;
ALTER TABLE "ActivityLoogGames" SET LOCALITY REGIONAL BY ROW;
SET override_multi_region_zone_config = true;
ALTER TABLE "ActivityLoogGames" CONFIGURE ZONE USING num_replicas = 3, constraints = '{+region=cn-hangzhou-aliyun: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}';
ALTER PARTITION "eu-fr-aws" OF INDEX "ActivityLoogGames"@"PK_ActivityLoogGames" CONFIGURE ZONE USING constraints = '{+region=eu-fr-aws: 1, +region=ap-sg-aws: 1, +region=us-siliconvalley-aliyun: 1}', num_replicas=COPY FROM PARENT;