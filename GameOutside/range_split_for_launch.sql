-- 策略说明:
-- 1. 每 10,000 个 PlayerId 创建一个 split point
-- 2. 覆盖范围: PlayerId 143000 到 350000 (留有余量)
-- 3. 为每个表设置不同的过期时间，避免同时合并造成性能波动
-- 4. 过期时间分散在 2025-12-01 到 2025-12-31 之间

-- =============================================================================
-- UserItems 表 (主键: crdb_region, PlayerId, ItemId, ShardId)
-- 过期时间: 2025-12-01 开始
-- =============================================================================
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 150000, 0, 2000) WITH EXPIRATION '2025-12-01 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 160000, 0, 2000) WITH EXPIRATION '2025-12-03 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 170000, 0, 2000) WITH EXPIRATION '2025-12-05 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 180000, 0, 2000) WITH EXPIRATION '2025-12-07 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 190000, 0, 2000) WITH EXPIRATION '2025-12-09 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 200000, 0, 2000) WITH EXPIRATION '2025-12-11 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 210000, 0, 2000) WITH EXPIRATION '2025-12-13 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 220000, 0, 2000) WITH EXPIRATION '2025-12-15 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 230000, 0, 2000) WITH EXPIRATION '2025-12-17 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 240000, 0, 2000) WITH EXPIRATION '2025-12-19 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 250000, 0, 2000) WITH EXPIRATION '2025-12-21 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 260000, 0, 2000) WITH EXPIRATION '2025-12-23 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 270000, 0, 2000) WITH EXPIRATION '2025-12-25 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 280000, 0, 2000) WITH EXPIRATION '2025-12-27 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 290000, 0, 2000) WITH EXPIRATION '2025-12-29 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 300000, 0, 2000) WITH EXPIRATION '2025-12-31 00:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 310000, 0, 2000) WITH EXPIRATION '2025-12-31 06:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 320000, 0, 2000) WITH EXPIRATION '2025-12-31 12:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 330000, 0, 2000) WITH EXPIRATION '2025-12-31 18:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 340000, 0, 2000) WITH EXPIRATION '2025-12-31 20:00:00';
ALTER TABLE "UserItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 350000, 0, 2000) WITH EXPIRATION '2025-12-31 22:00:00';

-- =============================================================================
-- UserAchievements 表 (主键: crdb_region, PlayerId, ConfigId, Target, ShardId)
-- 过期时间: 2025-12-02 开始 (错开1天)
-- =============================================================================
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 150000, 0, '', 2000) WITH EXPIRATION '2025-12-02 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 160000, 0, '', 2000) WITH EXPIRATION '2025-12-04 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 170000, 0, '', 2000) WITH EXPIRATION '2025-12-06 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 180000, 0, '', 2000) WITH EXPIRATION '2025-12-08 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 190000, 0, '', 2000) WITH EXPIRATION '2025-12-10 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 200000, 0, '', 2000) WITH EXPIRATION '2025-12-12 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 210000, 0, '', 2000) WITH EXPIRATION '2025-12-14 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 220000, 0, '', 2000) WITH EXPIRATION '2025-12-16 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 230000, 0, '', 2000) WITH EXPIRATION '2025-12-18 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 240000, 0, '', 2000) WITH EXPIRATION '2025-12-20 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 250000, 0, '', 2000) WITH EXPIRATION '2025-12-22 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 260000, 0, '', 2000) WITH EXPIRATION '2025-12-24 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 270000, 0, '', 2000) WITH EXPIRATION '2025-12-26 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 280000, 0, '', 2000) WITH EXPIRATION '2025-12-28 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 290000, 0, '', 2000) WITH EXPIRATION '2025-12-30 00:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 300000, 0, '', 2000) WITH EXPIRATION '2025-12-31 02:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 310000, 0, '', 2000) WITH EXPIRATION '2025-12-31 08:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 320000, 0, '', 2000) WITH EXPIRATION '2025-12-31 14:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 330000, 0, '', 2000) WITH EXPIRATION '2025-12-31 19:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 340000, 0, '', 2000) WITH EXPIRATION '2025-12-31 21:00:00';
ALTER TABLE "UserAchievements" SPLIT AT VALUES ('cn-hangzhou-aliyun', 350000, 0, '', 2000) WITH EXPIRATION '2025-12-31 23:00:00';

-- =============================================================================
-- UserFixedLevelMapProgress 表 (主键: crdb_region, PlayerId, MapId, ShardId)
-- 过期时间: 2025-12-03 开始 (错开2天)
-- =============================================================================
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 150000, 0, 2000) WITH EXPIRATION '2025-12-03 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 160000, 0, 2000) WITH EXPIRATION '2025-12-05 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 170000, 0, 2000) WITH EXPIRATION '2025-12-07 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 180000, 0, 2000) WITH EXPIRATION '2025-12-09 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 190000, 0, 2000) WITH EXPIRATION '2025-12-11 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 200000, 0, 2000) WITH EXPIRATION '2025-12-13 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 210000, 0, 2000) WITH EXPIRATION '2025-12-15 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 220000, 0, 2000) WITH EXPIRATION '2025-12-17 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 230000, 0, 2000) WITH EXPIRATION '2025-12-19 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 240000, 0, 2000) WITH EXPIRATION '2025-12-21 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 250000, 0, 2000) WITH EXPIRATION '2025-12-23 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 260000, 0, 2000) WITH EXPIRATION '2025-12-25 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 270000, 0, 2000) WITH EXPIRATION '2025-12-27 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 280000, 0, 2000) WITH EXPIRATION '2025-12-29 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 290000, 0, 2000) WITH EXPIRATION '2025-12-31 00:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 300000, 0, 2000) WITH EXPIRATION '2025-12-31 04:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 310000, 0, 2000) WITH EXPIRATION '2025-12-31 10:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 320000, 0, 2000) WITH EXPIRATION '2025-12-31 15:00:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 330000, 0, 2000) WITH EXPIRATION '2025-12-31 19:30:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 340000, 0, 2000) WITH EXPIRATION '2025-12-31 21:30:00';
ALTER TABLE "UserFixedLevelMapProgress" SPLIT AT VALUES ('cn-hangzhou-aliyun', 350000, 0, 2000) WITH EXPIRATION '2025-12-31 23:30:00';

-- =============================================================================
-- UserDailyStoreItems 表 (主键: crdb_region, PlayerId, ItemId, ShardId)
-- 过期时间: 2025-12-04 开始 (错开3天)
-- =============================================================================
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 150000, 0, 2000) WITH EXPIRATION '2025-12-04 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 160000, 0, 2000) WITH EXPIRATION '2025-12-06 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 170000, 0, 2000) WITH EXPIRATION '2025-12-08 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 180000, 0, 2000) WITH EXPIRATION '2025-12-10 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 190000, 0, 2000) WITH EXPIRATION '2025-12-12 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 200000, 0, 2000) WITH EXPIRATION '2025-12-14 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 210000, 0, 2000) WITH EXPIRATION '2025-12-16 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 220000, 0, 2000) WITH EXPIRATION '2025-12-18 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 230000, 0, 2000) WITH EXPIRATION '2025-12-20 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 240000, 0, 2000) WITH EXPIRATION '2025-12-22 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 250000, 0, 2000) WITH EXPIRATION '2025-12-24 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 260000, 0, 2000) WITH EXPIRATION '2025-12-26 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 270000, 0, 2000) WITH EXPIRATION '2025-12-28 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 280000, 0, 2000) WITH EXPIRATION '2025-12-30 00:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 290000, 0, 2000) WITH EXPIRATION '2025-12-31 01:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 300000, 0, 2000) WITH EXPIRATION '2025-12-31 05:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 310000, 0, 2000) WITH EXPIRATION '2025-12-31 11:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 320000, 0, 2000) WITH EXPIRATION '2025-12-31 16:00:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 330000, 0, 2000) WITH EXPIRATION '2025-12-31 19:45:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 340000, 0, 2000) WITH EXPIRATION '2025-12-31 21:45:00';
ALTER TABLE "UserDailyStoreItems" SPLIT AT VALUES ('cn-hangzhou-aliyun', 350000, 0, 2000) WITH EXPIRATION '2025-12-31 23:45:00';

-- =============================================================================
-- UserCards 表 (主键: crdb_region, PlayerId, CardId, ShardId)
-- 过期时间: 2025-12-05 开始 (错开4天)
-- =============================================================================
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 150000, 0, 2000) WITH EXPIRATION '2025-12-05 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 160000, 0, 2000) WITH EXPIRATION '2025-12-07 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 170000, 0, 2000) WITH EXPIRATION '2025-12-09 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 180000, 0, 2000) WITH EXPIRATION '2025-12-11 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 190000, 0, 2000) WITH EXPIRATION '2025-12-13 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 200000, 0, 2000) WITH EXPIRATION '2025-12-15 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 210000, 0, 2000) WITH EXPIRATION '2025-12-17 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 220000, 0, 2000) WITH EXPIRATION '2025-12-19 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 230000, 0, 2000) WITH EXPIRATION '2025-12-21 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 240000, 0, 2000) WITH EXPIRATION '2025-12-23 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 250000, 0, 2000) WITH EXPIRATION '2025-12-25 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 260000, 0, 2000) WITH EXPIRATION '2025-12-27 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 270000, 0, 2000) WITH EXPIRATION '2025-12-29 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 280000, 0, 2000) WITH EXPIRATION '2025-12-31 00:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 290000, 0, 2000) WITH EXPIRATION '2025-12-31 03:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 300000, 0, 2000) WITH EXPIRATION '2025-12-31 07:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 310000, 0, 2000) WITH EXPIRATION '2025-12-31 12:30:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 320000, 0, 2000) WITH EXPIRATION '2025-12-31 17:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 330000, 0, 2000) WITH EXPIRATION '2025-12-31 20:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 340000, 0, 2000) WITH EXPIRATION '2025-12-31 22:00:00';
ALTER TABLE "UserCards" SPLIT AT VALUES ('cn-hangzhou-aliyun', 350000, 0, 2000) WITH EXPIRATION '2025-12-31 23:50:00';


-- SHOW RANGES FROM TABLE "UserItems";

-- SHOW RANGES FROM TABLE "UserAchievements";

-- SHOW RANGES FROM TABLE "UserFixedLevelMapProgress";

-- SHOW RANGES FROM TABLE "UserDailyStoreItems";

-- SHOW RANGES FROM TABLE "UserCards";
