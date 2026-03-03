using System.Collections;
using System.Reflection;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.PayService;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using GameOutside.Services.KafkaConsumers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserTreasureBox = GameOutside.Models.UserTreasureBox;
using ChillyRoom.Infra.PlatformDef.DBModel.Repositories;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;

namespace GameOutside.Controllers;

public class TestController(
    IConfiguration configuration,
    ILogger<TestController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    LeaderboardModule leaderboardModule,
    PlayerModule playerModule,
    ActivityService activityService,
    UserRankService userRankService,
    UserEndlessRankService userEndlessRankService,
    DivisionService divisionService,
    SeasonService seasonService,
    UserInfoService userInfoService,
    GameService gameService,
    AntiCheatService antiCheatService,
    IPaidOrderWithShardRepository paidOrderWithShardRepository,
    IServiceProvider serviceProvider,
    UserCardService userCardService,
    IapPackageService iapPackageService,
    ExportGameDataService exportGameDataService,
    BattlePassService battlePassService,
    UserAssetService userAssetService,
    UserAchievementService userAchievementService
    )
    : BaseApiController(configuration)
{
    [HttpPost]
    public async Task<ActionResult<int>> CheckBinarySearch(int timeInt)
    {
        var scoreRewardList = serverConfigService.GetScoreRewardList();
        var targetRewardLevel = -1;
        var found = false;
        var count = scoreRewardList.Count;
        for (var i = 0; i < count; ++i)
        {
            var currentRewardInfo = scoreRewardList[i];
            if (currentRewardInfo.time > timeInt)
            {
                found = true;
                break;
            }

            targetRewardLevel = i;
        }

        if (!found)
            targetRewardLevel = count - 1;
        return targetRewardLevel;
    }

    [HttpPost]
    public async Task<ActionResult<int>> AddFakeScoreToLeaderBoard()
    {
        var random = new Random();
        var userList = await context.UserInfos.Where(u => true).ToListAsync();
        int totalCount = 0;
        foreach (var user in userList)
        {
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(user.ShardId, user.PlayerId);
            if (userAsset == null)
                continue;
            var score = random.NextInt64(100000, (long)int.MaxValue * 2);
            await gameService.ProcessScoreUpload(score, TimeUtils.GetCurrentTime(), true, user.PlayerId, user.ShardId,
                LeaderboardModule.NormalModeLeaderBoardId);

            var p = random.Next(0, 3);
            switch (p)
            {
                case 0:
                    score = random.NextInt64(100000, (long)int.MaxValue * 2);
                    await userEndlessRankService.UploadEndlessScoreAsync(user.ShardId, user.PlayerId, score,
                        TimeUtils.GetCurrentTime(), "survivor");
                    break;
                case 1:
                    score = random.NextInt64(100000, (long)int.MaxValue * 2);
                    await userEndlessRankService.UploadEndlessScoreAsync(user.ShardId, user.PlayerId, score,
                        TimeUtils.GetCurrentTime(), "towerdefence");
                    break;
                case 2:
                    score = random.NextInt64(100000, (long)int.MaxValue * 2);
                    await userEndlessRankService.UploadEndlessScoreAsync(user.ShardId, user.PlayerId, score,
                        TimeUtils.GetCurrentTime(), "trueendless");
                    break;
            }
            totalCount++;
        }

        await context.SaveChangesAsync();
        return Ok(totalCount);
    }

    private PayEventHandler PayEventHandler
        => serviceProvider.GetServices<IHostedService>().OfType<PayEventHandler>().Single();

    [HttpPost]
    public async Task<ActionResult<bool>> FakeRefund(long orderId)
    {
        short shardId = 1051;
        long playerId = 19009;
        long userId = 44543899;
        MethodInfo? methodOnOrderRefund
            = typeof(PayEventHandler).GetMethod("OnOrderRefund", BindingFlags.NonPublic | BindingFlags.Instance);
        if (methodOnOrderRefund == null)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());

        var paidOrder = await paidOrderWithShardRepository.GetPaidOrderByOrderIdAsync(orderId, shardId, TrackingOptions.NoTracking, StaleReadOptions.NoStaleRead);
        if (paidOrder is null)
        {
            return BadRequest(ErrorKind.INVALID_INPUT.Response());
        }

        var orderStatusEvent = new OrderStatusEvent()
        {
            OrderStatus = 0,
            OrderId = orderId,
            UserId = userId,
            PlayerId = playerId,
            Payload = paidOrder.Payload,
            Quantity = 1,
            SkuType = 1,
        };


        // 测试用的，orderId暂时用时间戳
        var task = (ValueTask)methodOnOrderRefund.Invoke(PayEventHandler, [orderStatusEvent, CancellationToken.None])!;
        await task;
        return Ok(orderId);
    }

    #region 添加假的用户信息

    public record struct FakeUserDataArg(int startId, int endId);

    [HttpPost]
    public async Task<ActionResult<bool>> MakeFakeUserData(FakeUserDataArg arg)
    {
        List<string> nameList = new List<string>()
        {
            "凡菱桑",
            "绿海姑娘",
            "岭南骊红",
            "南风冰绿",
            "留云彤彤",
            "傲寒骊媛",
            "七步孟乐",
            "北陆忆莲",
            "西域凝雁",
            "东风谷正祥",
            "是你的甲子呀",
            "志高君",
            "魂魄之梦",
            "五河青曼",
            "黑山宾实",
            "天际依竹",
            "寂灭正真",
            "玄冰祺福",
            "八坂利彬",
            "璃月蕴涵",
            "不灭奕洳",
            "万花乐容",
            "云来曼霜",
            "桃花晓枝",
            "是滢渟吖",
            "伏魔俊贤",
            "晨羲超级甜",
            "北斗芸馨",
            "无定海阳",
            "语冰超级甜",
            "一只若枫呀",
            "长安颀秀",
            "海燕殿下",
            "南川炜曦",
            "星熊诗霜",
            "是你的志远呀",
            "巴蜀欣嘉",
            "北海增芳",
            "天神院婉清",
            "明钰小姐姐",
            "七伤思聪",
            "八坂慧研",
            "碧桃天烟",
            "暗月寺惜天",
            "盼波氏",
            "香风倩云",
            "風見阳曜",
            "蓉沼倩美",
            "北海宏儒",
            "风满爱朵",
            "无妄小枫",
            "水桥腾骞",
            "北辰酱大魔王",
            "碧水月桃",
            "玄虚芬芬",
            "是闵雨吖",
            "天罡幼菱",
            "英博酱大魔王",
            "法慧素昕",
            "菩提朝宇",
            "小野寺勇捷",
            "文静小郎君",
            "运升来了",
            "幽花建霞",
            "江潜公子",
            "松风蔚然",
            "终幕黎明",
            "七星运来",
            "销魂树泽",
            "犬走泽语",
            "飞龙昂雄",
            "保胜郎",
            "風見逸秀",
            "蓬莱山蕴和",
            "四风莎莉",
            "邻家芳芳",
            "风陵展鹏",
            "星熊艳清",
            "醉巧来了",
            "一条小明达",
            "东润三岁啦",
            "卧龙静雅",
            "宇荫来了",
            "澄静若骞",
            "烈焰夏旋",
            "百花驰丽",
            "元翠少爷",
            "努力啊大毅君",
            "长拳多思",
            "日光代桃",
            "无妄英武",
            "怜珍小姐姐",
            "御阪弘大",
            "葵花书娟",
            "震雷夜绿",
            "努力啊大恒硕",
            "截手语桃",
            "震雷长海",
            "风神琰琬",
            "璃月乐英",
            "风见从之",
            "五河明杰",
            "龙爪阳旭",
            "疯魔向薇",
            "折梅摄提格",
            "一只昕昕呀",
            "蓬莱山湛芳",
            "月河顺红",
            "云居金钟",
            "金山驰颖",
            "努力啊大以蕾",
            "幼霜小公主",
            "碧波晓山",
            "灵空寒烟",
            "凌波慧颖",
            "香风芷蓝",
            "遐思公子",
            "金蛇士媛",
            "邻家绮丽",
            "兴贤来了",
            "映秋公子",
            "平原向梦",
            "诗诗酱吖",
            "静心代芙",
            "四象安歌",
            "猫巷少女迎梅",
            "凌霄凡雁",
            "浩广子",
            "梦桐超级甜",
            "怜青公子",
            "一只醉柳呀",
            "兰花夏蝶",
            "梓珊小公主",
            "香风海白",
            "泄矢项禹",
            "新峰可爱吗",
            "江南欢欣",
            "留云幼儿",
            "宫古叶农",
            "是你的凡双呀",
        };

        for (int id = arg.startId; id <= arg.endId; id++)
        {
            var random = new Random(DateTime.Now.Second + id);
            var index = random.Next(0, nameList.Count);

            userInfoService.CreateNewUserInfo(new UserInfo()
            {
                // Notice
                ShardId = 2000,
                UserId = id,
                PlayerId = id,
                Signature = $"用户[{id}]没啥说的",
            });
        }

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(true);
    }

    record struct FakeGameEndMessageReply(string message, string hash);

    [HttpPost]
    public async Task<ActionResult<string>> MakeFakeGameEndMessage(string content)
    {
        var encryptedContent = EncryptHelper.EncryptHelper.DesEncrypt(content, "jO*&}.;H");
        var contentHash = EncryptHelper.EncryptHelper.CustomHash(content);
        return Ok(new FakeGameEndMessageReply(encryptedContent, contentHash));
    }

    [HttpPost]
    public async Task<ActionResult<bool>> SetUserHeroes(int playerId, List<int> heroList)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var assets = await userAssetService.GetUserAssetsSimpleAsync(shardId.Value, playerId);
        if (assets == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        assets.Heroes = heroList;
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok("ok");
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateRandomUserRank(int playerId)
    {
        // 上传最高分
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var scoreLong = 99999L;
        var userRank = await userRankService.GetCurrentSeasonUserRankByDivisionAsync(shardId.Value, playerId, 1);
        if (userRank == null)
        {
            var division = await divisionService.GetDivisionNumberAsync(shardId.Value, playerId, CreateOptions.CreateWhenNotExists);
            userRank = await userRankService.CreateUserRankAsync(shardId.Value, playerId, division, scoreLong, 0, false);
        }
        if (userRank.HighestScore < scoreLong)
        {
            userRank.HighestScore = scoreLong;
        }

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return 0;
    }

    [HttpPost]
    public async Task<ActionResult<UserTreasureBox>> AddTreasureBox(
        long playerId,
        int itemId,
        int itemCount,
        int starCount)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var assets = await userAssetService.GetUserAssetsDetailedAsync(shardId.Value, playerId);
        var box = userItemService.CreateUserTreasureBoxData(itemId, shardId.Value, playerId, itemCount, starCount);
        if (box != null)
        {
            assets.UserTreasureBoxes.Add(box);
            try
            {
                await context.SaveChangesWithDefaultRetryAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
            }

            return Ok(box);
        }

        return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
    }

    [HttpPost]
    public async Task<ActionResult<string>> MakeAttendanceData()
    {
        var userList = await context.UserInfos.Where(u => true).ToListAsync();
        if (userList.Count <= 0)
            return Ok("yes");

        foreach (var user in userList)
        {
            var userAttendanceRecord = await context.GetUserAttendanceRecord(user.ShardId, user.PlayerId);
            if (userAttendanceRecord != null)
                continue;
            context.CreateUserAttendanceRecord(user.ShardId, user.PlayerId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok("yes");
    }


    [HttpPost]
    public async Task<ActionResult<string>> MakeFakeRankData()
    {
        long idStart = 100000;
        int[] rankUserList = new[] { 3000, 1000, 500, 300, 100, 100 };
        // int[] rankUserList = new[] {60, 60, 60, 60, 60, 60};
        var random = new Random();
        var seasonNumber = seasonService.GetCurrentSeasonNumber();
        // 正常rank榜
        for (int i = 0; i < serverConfigService.TotalDivisionCount; i++)
        {
            int division = i + 1;
            int userCount = rankUserList[i];
            var divisionScore = 0;
            for (int j = 0; j < userCount; j++)
            {
                var score = random.NextInt64(1000, 1000000);
                long playerId = idStart++;
                short shardId = 2000;
                await userRankService.CreateUserRankAsync(shardId, playerId, division, score, 0, false);
                divisionService.CreateUserDivision(shardId, playerId, divisionScore);
                // 有一半的概率刷无尽榜
                if (random.Next(0, 2) == 0)
                {
                    userEndlessRankService.CreateUserEndlessRank(shardId, playerId, seasonNumber);
                }

                try
                {
                    await context.SaveChangesWithDefaultRetryAsync();
                }
                catch (Exception e)
                {
                    logger.LogError(e, e.Message);
                    return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
                }
            }
        }

        return Ok("yes");
    }

    [HttpPost]
    public async Task<ActionResult<List<float>>> TestRandomCardQualityRatio(int testCount, List<int> weightList)
    {
        var config = new TreasureBoxConfig() { card_count = 100, different_card_count = 5, weight_list = weightList };
        var totalQualityCountList = new List<float>();
        for (int i = 0; i < config.weight_list.Count; i++)
            totalQualityCountList.Add(0);
        for (int randomTimes = 0; randomTimes < testCount; randomTimes++)
        {
            var qualityCardCountList = RandomQualityCountList(config);

            for (int i = 0; i < config.weight_list.Count; i++)
                totalQualityCountList[i] += qualityCardCountList[i];
        }

        for (int i = 0; i < config.weight_list.Count; i++)
            totalQualityCountList[i] /= testCount * 100;

        return Ok(totalQualityCountList);
    }

    private List<int> RandomQualityCountList(TreasureBoxConfig config)
    {
        var random = new Random();
        var qualityCardCountList = Enumerable.Repeat(0, config.weight_list.Count).ToList();
        var accumulateWeightList = Enumerable.Repeat(0, config.weight_list.Count).ToList();
        for (int i = 0; i < config.weight_list.Count; i++)
            for (int j = 0; j <= i; j++)
                accumulateWeightList[i] += config.weight_list[j];
        var weightSum = config.weight_list.Sum();
        for (int i = 0; i < config.card_count; ++i)
        {
            var randomValue = random.Next(0, weightSum);
            ++qualityCardCountList[accumulateWeightList.UpperBound(randomValue, (a, b) => a - b)];
        }

        return qualityCardCountList;
    }

    public class RandomCardTestArg
    {
        public int CardCount { get; set; }
        public List<int> WeightList { get; set; }
        public int DifferentCardCount { get; set; }
        public List<int> GuaranteeList { get; set; }
    }

    public record struct TestRandomCardResult(
        List<int> QualityCardCountList,
        List<List<int>> DifferentCardList,
        int TotalCount);

    [HttpPost]
    public async Task<ActionResult<TestRandomCardResult>> TestRandomCardList(RandomCardTestArg arg)
    {
        var config = new TreasureBoxConfig()
        {
            card_count = arg.CardCount,
            different_card_count = arg.DifferentCardCount,
            weight_list = arg.WeightList,
            guarantee_count_list = arg.GuaranteeList
        };
        // 计算不同品质的卡牌数量
        var qualityCardCountList = RandomQualityCountList(config);
        // 计算保底
        var totalAddedGuaranteeCount = 0;
        for (int i = config.guarantee_count_list.Count - 1; i > 0; --i)
        {
            if (qualityCardCountList[i] >= config.guarantee_count_list[i])
                continue;
            var diff = config.guarantee_count_list[i] - qualityCardCountList[i];
            totalAddedGuaranteeCount += diff;
            qualityCardCountList[i] = config.guarantee_count_list[i];
        }

        qualityCardCountList[0] -= totalAddedGuaranteeCount;
        qualityCardCountList[0] = qualityCardCountList[0] < 0 ? 0 : qualityCardCountList[0];
        // 分配到不同的卡牌数量上
        var differentCardCountByQuality = new List<List<int>>();
        var differentCardRatio = config.different_card_count / (double)config.card_count;
        var left = config.different_card_count;
        for (int quality = qualityCardCountList.Count - 1; quality >= 0; --quality)
        {
            var totalCardCount = qualityCardCountList[quality];
            var differentCardCount = (int)Math.Ceiling(totalCardCount * differentCardRatio);
            differentCardCount = differentCardCount > left ? left : differentCardCount;
            if (differentCardCount <= 0 && totalCardCount > 0)
                differentCardCount = 1;
            left -= differentCardCount;
            var cardCountList = new List<int>();
            var totalCardLeft = totalCardCount;
            for (int i = 0; i < differentCardCount; i++)
            {
                var cardCount = totalCardCount / differentCardCount;
                cardCount = cardCount > totalCardLeft ? totalCardLeft : cardCount;
                totalCardLeft -= cardCount;
                cardCountList.Add(cardCount);
            }

            for (int i = 0; i < totalCardLeft; i++)
                ++cardCountList[i];
            differentCardCountByQuality.Add(cardCountList);
        }

        differentCardCountByQuality.Reverse();
        // 确认卡牌


        var total = differentCardCountByQuality.Sum(item => item.Sum());

        // 确认卡牌
        return Ok(new TestRandomCardResult()
        {
            QualityCardCountList = qualityCardCountList,
            DifferentCardList = differentCardCountByQuality,
            TotalCount = total
        });
    }

    #endregion

    #region Debug用

    [HttpGet]
    public async Task<ActionResult<string>> DeleteUserRecords(long playerId)
    {
        exportGameDataService.DeleteUserRecords(playerId);
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok("yes");
    }

    [HttpGet]
    public async Task<ActionResult<string>> UnlockAllFixedLevel(long playerId, short shardId)
    {
        var fixedMapList = serverConfigService.GetFixedMapConfigList();
        foreach (var fixedMapConfig in fixedMapList)
        {
            var fixedMapProgress = await context.GetUserFixedLevelMapProgress(playerId, shardId, fixedMapConfig.id);
            if (fixedMapProgress == null)
            {
                await context.AddUserFixedLevelMapProgress(new UserFixedLevelMapProgress()
                {
                    MapId = fixedMapConfig.id, PlayerId = playerId, ShardId = shardId, StarCount = 3
                });
            }
            else
            {
                fixedMapProgress.StarCount = 3;
            }
        }
        await context.SaveChangesAsync();
        return Ok("yes");
    }

    [HttpPost]
    public async Task<ActionResult<string>> MakeFakeUsersAndSurvivorRanks()
    {
        var seasonNumber = seasonService.GetCurrentSeasonNumber();
        for (int i = 0; i < 50; i++)
        {
            short shardId = 1051;
            var userId = 5899 + i;
            var playerId = 5899 + i;
            userInfoService.CreateNewUserInfo(new UserInfo()
            {
                ShardId = shardId,
                UserId = userId,
                PlayerId = playerId,
                Signature = "account sdk is so fucking shit!"
            });

            var prng = Random.Shared;
            var userEndlessRank = userEndlessRankService.CreateUserEndlessRank(shardId, playerId, seasonNumber);
            userEndlessRank.SurvivorScore = prng.Next(0, 100000000);
            userEndlessRank.SurvivorTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            userEndlessRank.TowerDefenceScore = prng.Next(0, 100000000);
            userEndlessRank.TowerDefenceTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            userEndlessRank.TrueEndlessScore = prng.Next(0, 100000000);
            userEndlessRank.TrueEndlessTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok("yes");
    }


    [HttpPost]
    public async Task<ActionResult<string>> ReplacePlayerSpec(long playerId, GmController.UserSpec userSpec)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (shardId == 0)
            shardId = PlayerShard;
        // _context.DeleteUserRecords(playerId);
        // try
        // {
        //     await _context.SaveChangesWithDefaultRetryAsync();
        // }
        // catch (Exception e)
        // {
        //     _logger.LogError(e, e.Message);
        //     return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        // }

        // 替换所有 PlayerId 和 ShardId
        foreach (var prop in userSpec.GetType().GetProperties())
        {
            var value = prop.GetValue(userSpec);
            if (value == null) continue;

            if (prop.PropertyType.GetProperties().Any(p => p.Name == "PlayerId"))
            {
                prop.PropertyType.GetProperty("PlayerId")?.SetValue(value, playerId);
                prop.PropertyType.GetProperty("ShardId")?.SetValue(value, shardId);
            }
            // 处理列表
            else if (value is IEnumerable enumerable && value is not string)
            {
                foreach (var item in enumerable)
                {
                    if (item?.GetType().GetProperties().Any(p => p.Name == "PlayerId") == true)
                    {
                        item.GetType().GetProperty("PlayerId")?.SetValue(item, playerId);
                        item.GetType().GetProperty("ShardId")?.SetValue(item, shardId);
                    }
                }
            }
        }

        if (userSpec.UserAssets != null)
        {
            foreach (var treasureBox in userSpec.UserAssets.UserTreasureBoxes)
                treasureBox.Id = Guid.NewGuid();
        }

        if (userSpec.UserInfo != null)
        {
            foreach (var history in userSpec.UserInfo.Histories)
            {
                history.Id = Guid.NewGuid();
            }
        }

        foreach (var record in userSpec.UserIapPurchaseRecords)
        {
            record.Id = Guid.NewGuid();
        }

        exportGameDataService.DeleteUserRecords(playerId);
        exportGameDataService.AddUserSpec(userSpec);

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok("yes");
    }

    public static void ReplaceFieldsValue(object target, string targetFieldName, object newValue)
    {
        Type type = target.GetType();

        // 遍历所有字段（包含私有字段）
        foreach (FieldInfo field in
                 type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            // 匹配字段名
            if (field.Name == targetFieldName)
            {
                // 类型安全检查
                if (!field.FieldType.IsInstanceOfType(newValue))
                    throw new InvalidCastException($"类型不匹配: {field.FieldType} vs {newValue.GetType()}");

                field.SetValue(target, newValue);
            }
            else
            {
                // 递归处理嵌套字段
                object? fieldValue = field.GetValue(target);
                if (fieldValue != null)
                {
                    if (!field.FieldType.IsValueType)
                    {
                        ReplaceFieldsValue(fieldValue, targetFieldName, newValue);
                    }
                }
            }
        }
    }

    [HttpPost]
    public async Task<ActionResult<string>> PassAllFixedMapLevel(long playerId)
    {
        var fixedMapConfigList = serverConfigService.GetFixedMapConfigList();
        foreach (var config in fixedMapConfigList)
        {
            var progress = await context.GetUserFixedLevelMapProgress(playerId, 100, config.id);
            if (progress == null)
            {
                progress = new UserFixedLevelMapProgress()
                {
                    MapId = config.id,
                    PlayerId = playerId,
                    ShardId = 100,
                    StarCount = 0
                };
                await context.AddUserFixedLevelMapProgress(progress);
            }

            if (config.IsTrainLevel)
                progress.StarCount = 1;
            else
                progress.StarCount = 3;
        }

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok("yes");
    }

    [HttpPost]
    public async Task<UserInfo[]> BatchGetUserInfos()
    {
        long[] ids = [19030, 19022];
        return (await userInfoService.BatchGetUserInfosByPlayerIdsAsync(ids)).Values.ToArray();
    }

    #endregion

    #region 压测用

    [HttpPost]
    public async Task<ActionResult<bool>> InsertAverageAccount(long userId, long playerId)
    {
        short shard = 1051;
        var userInfo = await userInfoService.GetUserInfoAsync(shard, playerId);
        if (userInfo is not null)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());
        try
        {
            var newUser = await context.WithRCUDefaultRetry(async _ =>
            {
                var newUser = userInfoService.CreateNewUserInfo(new UserInfo
                {
                    Signature = ":-)",
                    ShardId = shard,
                    PlayerId = playerId,
                    UserId = userId,
                    HideHistory = false,
                });
                // 创建资产条目
                var (newCardList, userAsset) = await userItemService.CreateDefaultUserAssets(shard, playerId, 0, GameVersion);
                userAsset.CoinCount = 9999999;
                userAsset.DiamondCount = 9999999;
                var fightCards = serverConfigService.GetFightCardItemConfigList();
                var buildingCards = serverConfigService.GetBuildingCardItemConfigList();
                var itemList = fightCards.Select(config => config.id).Union(
                    buildingCards.Select(config => config.id)).ToList();
                var countList = itemList.Select(id => 9999).ToList();
                var result = await userItemService.UnpackItemList(userAsset, itemList, countList, GameVersion);
                if (result == null)
                    return null;
                userAsset.LevelData = new UserLevelData() { Level = 50, LevelScore = 999, RewardStatusList = new() };
                userAssetService.AddUserAssetAsync(userAsset);
                // 创建开宝箱相关数据
                gameService.AddUserGameInfoById(shard, playerId);
                // 创建推广相关数据
                iapPackageService.AddPromotionData(playerId, shard);
                // 创建聚宝信息
                context.AddUserIdleRewardInfo(shard, playerId);
                // 创建新手任务
                UserBeginnerTask beginnerTask = new UserBeginnerTask()
                {
                    ShardId = shard,
                    PlayerId = playerId,
                    FinishedCount = 0,
                    Received = false,
                    StartTime = TimeUtils.GetCurrentTime(),
                    DayIndex = 0,
                    TaskList = new(),
                };

                // 随机三个任务
                var configList = serverConfigService.GetBeginnerTaskList();
                var selectionList = configList.Where(config => !config.key.Equals("soldier_max_level") || beginnerTask.DayIndex >= 2).ToList();
                var beginnerTaskConfig = serverConfigService.GetBeginnerTaskDayConfig(beginnerTask.DayIndex);
                var taskList = new List<BeginnerTaskData>();
                int i = 0;
                if (beginnerTaskConfig != null)
                    for (; i < 3 && i < beginnerTaskConfig.predefined_tasks.Length; i++)
                    {
                        var predefinedTask = serverConfigService.GetBeginnerTaskConfig(beginnerTaskConfig.predefined_tasks[i]);
                        selectionList.RemoveAll(config => config.key == predefinedTask.key);
                        taskList.Add(new BeginnerTaskData() { Id = predefinedTask.id, Progress = 0, });
                    }
                for (; i < 3 && selectionList.Count > 0; i++)
                {
                    var randomOne = selectionList.WeightedRandomSelectOne(_ => 10)!;
                    selectionList.RemoveAll(config => config.key == randomOne.key);

                    taskList.Add(new BeginnerTaskData() { Id = randomOne.id, Progress = 0, });
                }
                beginnerTask.TaskList = taskList;
                context.Entry(beginnerTask).Property(t => t.TaskList).IsModified = true;

                context.AddBeginnerTask(beginnerTask);
                // 创建签到数据
                context.CreateUserAttendanceRecord(shard, playerId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                // 活动数据
                var openingActivity = activityService.GetOpeningActivities("100.0.0");
                foreach (var activity in openingActivity)
                {
                    switch (activity.activity_type)
                    {
                        case ActivityType.ActivityLuckyStar:
                        {
                            activityService.AddActivityLuckyStarData(playerId, shard, activity.id);
                            break;
                        }
                        case ActivityType.ActivityFortuneBag:
                        {
                            activityService.AddUserFortuneBagInfo(playerId, shard, activity.id);
                            break;
                        }
                        case ActivityType.ActivityUnrivaledGod:
                        {
                            await activityService.CreateDefaultUnrivaledGodDataAsync(playerId, shard, activity.id);
                            break;
                        }
                        case ActivityType.ActivityCoopBoss:
                        {
                            activityService.CreateDefaultCoopBossData(playerId, shard, activity.id);
                            break;
                        }
                        case ActivityType.ActivityTreasureMaze:
                        {
                            activityService.CreateTreasureMazeData(playerId, shard, activity.id, 0);
                            break;
                        }
                        case ActivityType.ActivityEndlessChallenge:
                        {
                            activityService.CreateDefaultEndlessChallengeData(playerId, shard, activity.id);
                            break;
                        }
                    }
                }

                // 跳过教程
                await context.SetUserIntData(shard, playerId,
                    "7|88|13|-1|10|1|22|53|23|315|17|3|11|1|14|13|28|13|29|3|32|10|39|8|9|1|47|1|15|3|20|1|5|2");
                await context.SetUserStrData(shard, playerId,
                    "38|0;1;3;1|21|2;6;40;1;18;1;52;1;17;1;8;1;56;300|45||16|3;0;0;0;2;0;1;0|8|9&3-3");

                // 创建战役通关信息
                foreach (var fixedGameConfig in serverConfigService.GetFixedMapConfigList())
                    if (fixedGameConfig.id < 100)
                        await context.AddUserFixedLevelMapProgress(new UserFixedLevelMapProgress()
                        {
                            MapId = fixedGameConfig.id,
                            PlayerId = playerId,
                            ShardId = shard,
                            StarCount = 1,
                            FinishedTaskList = new() { 0 }
                        });

                await using var transaction = await context.Database.BeginTransactionAsync();
                await context.SaveChangesAsync(false);
                await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(result.NewCardList, shard, playerId);
                await transaction.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();
                return newUser;
            });

            return Ok(true);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }
    }
    [HttpGet]
    public async Task<ActionResult<List<int>>> GetAllUpgradableCards(long playerId)
    {
        short shard = 1051;
        var userCards = await userCardService.GetReadonlyUserCardsAsync(shard, playerId);
        if (userCards is null)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        List<int> result = new();
        foreach (var card in userCards)
        {
            var buildingConfig = serverConfigService.GetBuildingCardConfig(card);
            if (buildingConfig != null)
            {
                if (card.CardLevel >= buildingConfig.level_exp_list.Count - 1)
                    continue;
                if (card.CardExp < buildingConfig.level_exp_list[card.CardLevel])
                    continue;
                result.Add(card.CardId);
                continue;
            }

            var unitConfig = serverConfigService.GetFightCardConfig(card);
            if (unitConfig != null)
            {
                if (unitConfig.IsMaxLevel)
                    continue;
                if (card.CardExp < unitConfig.upgrade_need_exp)
                    continue;
                result.Add(card.CardId);
            }
        }
        return Ok(result);
    }
    [HttpPost]
    public async Task<ActionResult<List<Guid>>> RandomlyAddTreasureBox(long playerId, int count)
    {
        short shard = 1051;
        var userAsset = await userAssetService.GetUserAssetsSimpleAsync(shard, playerId);
        if (userAsset == null)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        int[] boxPool = new[] { 54001, 54002, 54003 };
        var timeStamp = TimeUtils.GetCurrentTime();
        int boxId = boxPool[timeStamp % boxPool.Length];
        GeneralReward boxReward = new() { ItemList = new(), CountList = new() };
        boxReward.ItemList.Add(boxId);
        boxReward.CountList.Add(count);
        var (newCardList, reply) = await userItemService.TakeReward(userAsset, boxReward, GameVersion);
        if (reply != null)
        {
            var boxChange = reply.AssetsChange?.TreasureBoxChange;
            if (boxChange != null)
            {
                try
                {
                    await using var t = await context.Database.BeginTransactionAsync();
                    await context.SaveChangesWithDefaultRetryAsync(false);
                    var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, shard, playerId);
                    await t.CommitAsync();
                    context.ChangeTracker.AcceptAllChanges();
                }
                catch (Exception e)
                {
                    logger.LogError(e, e.Message);
                    return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
                }
                return Ok(boxChange?.AddList.Select(box => box.Id).ToList());
            }
        }
        return BadRequest(ErrorKind.INVALID_INPUT.Response());
    }

    [HttpPost]
    public async Task<ActionResult<bool>> FakeGameEndMessage(long userId, long playerId)
    {
        short shard = 1051;
        var timeStamp = TimeUtils.GetCurrentTime();
        int idx = 0;
        var taskData = await context.GetBeginnerTaskAsync(shard, playerId);
        //if (taskData == null)
        //    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        NormalGameEndMessage gameEndMessage = GenerateRandonGameEndMessage(playerId, timeStamp, taskData);
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
        
        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateNormalAndFixGameScore(gameEndMessage, true, playerId, currentSeasonNumber);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(playerId, gameEndMessage);
        cheatingList.Clear();

        // ============================ 计算战令经验 ============================ //
        var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, true, currentSeasonNumber);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<bool>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(shard, playerId);
            if (userGameInfo == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            if (!gameService.IsMessageObjValid(userGameInfo, gameEndMessage) && false)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo,
                playerId, shard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 通用游戏结束处理和分数上传 ============================ //
            var (division, commonError) = await gameService.ProcessCommonGameEndLogicWithScore(battleExpTotal,
                gameEndMessage.Win, scoreLong, gameEndMessage.TimeStamp, playerId, shard, LeaderboardModule.NormalModeLeaderBoardId);
            if (commonError != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = commonError });

            // ============================ 计算星星 ============================ //
            var fixedMapConfig = serverConfigService.GetFixedMapConfig(gameEndMessage.MapId);
            if (fixedMapConfig is null)
                return BadRequest(ErrorKind.NO_MAP_CONFIG.Response());
            var (startResult, error2) = gameService.CalculateTaskStars(gameEndMessage, fixedMapConfig);
            if (error2 != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error2 });
            var starCount = 0;
            var taskIdList = new List<int>();
            var starStatus = new List<bool>();
            var succeedTaskList = new List<int>();
            foreach ((int taskId, bool hasStar) in startResult)
            {
                taskIdList.Add(taskId);
                starStatus.Add(hasStar);
                starCount += hasStar ? 1 : 0;
                if (hasStar)
                    succeedTaskList.Add(taskId);
            }

            var customArgs = new FixedMapGameEndCustomArgs(taskIdList, starStatus);
            // ============================ 发放星星 ============================ //
            var fixedLevelMapProgress =
                await context.GetUserFixedLevelMapProgress(playerId, shard, fixedMapConfig.id);
            var oldStars = 0;
            if (fixedLevelMapProgress is null)
            {
                fixedLevelMapProgress = new UserFixedLevelMapProgress
                {
                    MapId = fixedMapConfig.id,
                    PlayerId = playerId,
                    ShardId = shard,
                    StarCount = starCount,
                };
                await context.AddUserFixedLevelMapProgress(fixedLevelMapProgress);
                fixedLevelMapProgress.FinishedTaskList = succeedTaskList;
                oldStars = 0;
            }
            else
            {
                oldStars = fixedLevelMapProgress.StarCount;
                fixedLevelMapProgress.StarCount = fixedLevelMapProgress.StarCount < starCount
                    ? starCount
                    : fixedLevelMapProgress.StarCount;
                if (fixedLevelMapProgress.StarCount <= starCount)
                    fixedLevelMapProgress.FinishedTaskList = succeedTaskList;
            }

            // ============================ 直接按照配置给固定奖励 ============================ //
            var generalReward = new GeneralReward() { ItemList = [], CountList = [] };
            if (oldStars <= 0 && fixedLevelMapProgress.StarCount > 0)
            {
                // 通关奖励
                generalReward.ItemList.AddRange(fixedMapConfig.item_list);
                generalReward.CountList.AddRange(fixedMapConfig.item_count_list);
            }

            // 如果是第一次通过1-1，额外发放历练之路奖励
            if (oldStars <= 0 && fixedMapConfig.id == 2)
            {
                generalReward.ItemList.Add(2);
                generalReward.CountList.Add(100);
            }

            // ============================ 发放奖励 ============================ //
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(shard, playerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var (newCardList, rewardError) = await gameService.AddGameEndReward(generalReward, userAsset,
                gameEndMessage, playerId, shard, "100.0.0");
            if (rewardError != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)rewardError });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 增加每日宝箱进度 ============================ //
            // 第一次过1-1写死了不增加每日宝箱进度
            if (gameEndMessage.Win &&
                serverConfigService.TryGetParameterInt(Params.BattleModeTreasureBoxProgressAdd, out int addValue) &&
                !(oldStars <= 0 && fixedMapConfig.id == 2))
            {
                await gameService.IncreaseDailyTreasureBoxProgress(playerId, shard, division, addValue, userAsset.TimeZoneOffset);
            }

            // ============================ 记录新难度解锁 ============================ //
            var difficultyChanges = new List<DifficultyChange>();
            if (gameEndMessage.Win)
            {
                var difficultyConfigList = serverConfigService.GetDifficultyConfigList();
                var difficultyData = userAsset.DifficultyData;
                for (int i = 0; i < difficultyConfigList.Count; i++)
                {
                    var difficultyConfig = difficultyConfigList[i];
                    if (difficultyConfig.unlock_fixed_map == -1 ||
                        gameEndMessage.MapId == difficultyConfig.unlock_fixed_map ||
                        oldStars > 0)
                        continue;
                    difficultyChanges.Add(new DifficultyChange() { Difficulty = i, Level = 0, Star = 0 });
                }
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(shard, playerId, gameEndMessage.GameStartTime,
                () => gameEndMessage.ToFixedMapUserHistory(shard, playerId, scoreLong, starCount));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });


            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, gameEndMessage.TaskRecords,
                shard,
                playerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY,
                gameEndMessage.KillCount,
                shard, playerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_STORY_MAP, 1,
                shard, playerId, userAsset.TimeZoneOffset);

            // 合并一下相同的奖励
            generalReward.DistinctAndMerge();

            // ============================ 是否符合礼包推销条件 ============================ //
            if (!serverConfigService.TryGetParameterInt(Params.GeneralPromotionUnlockMap, out var promotionUnlockLevelId) ||
               !serverConfigService.TryGetParameterInt(Params.PromotionShowInterval, out var packagePromotionShowInterval) ||
               !serverConfigService.TryGetParameterString(Params.PromotedPackageIapIdList, out var promotedPackageIapIds))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
            if (fixedMapConfig.id == promotionUnlockLevelId)
            {
                var userPromotionStatus = await iapPackageService.GetPromotionData(playerId, shard);
                if (userPromotionStatus == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
                // 向玩家推销破冰付费
                if (userPromotionStatus.IceBreakingPayPromotion == 0)
                    userPromotionStatus.IceBreakingPayPromotion = 1;
            }
            else if (!gameEndMessage.Win && fixedMapConfig.id > promotionUnlockLevelId && !fixedMapConfig.IsTrainLevel)
            {
                var userPromotionStatus = await iapPackageService.GetPromotionData(playerId, shard);
                if (userPromotionStatus == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
                var promotedPackageIapIdList = promotedPackageIapIds.Split('|').ToList();
                if ((userPromotionStatus.LastPromotedPackage == "" && userPromotionStatus.PackagePromotionTime == 0) ||
                    // 玩家从未被推销过限时礼包
                    (userPromotionStatus.LastPromotedPackage != "" &&
                     TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), userPromotionStatus.PackagePromotionTime, userAsset.TimeZoneOffset, 0) >= packagePromotionShowInterval))
                // 玩家未完成所有限时礼包的购买，且冷却时间到了
                {
                    if (userPromotionStatus.LastPromotedPackage == "")
                        userPromotionStatus.LastPromotedPackage = promotedPackageIapIdList[0];
                    userPromotionStatus.PackagePromotionTime = TimeUtils.GetCurrentTime();
                }
            }
            try
            {
                await using var t = await context.Database.BeginTransactionAsync();
                await context.SaveChangesWithDefaultRetryAsync(false);
                // ============================ 记录成就 ============================ //
                var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                    gameEndMessage.AchievementRecords,
                    shard, playerId);
                achievementChange = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList,
                    shard, playerId);
                await t.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
            }

            return Ok(true);
        });
    }

    private NormalGameEndMessage GenerateRandonGameEndMessage(long playerId, long timeStamp, UserBeginnerTask? taskData)
    {
        Random random = new Random();
        NormalGameEndMessage gameEndMessage = new();
        var mapList = serverConfigService.GetFixedMapConfigList();

        gameEndMessage.TimeStamp = timeStamp;
        gameEndMessage.RoomId = timeStamp;
        gameEndMessage.BuildingList = new();
        gameEndMessage.ResourceScoreList = new() {
            new GameEndResourceScore()
        {
            PlayerId = playerId, Score = 99999,
        }};
        gameEndMessage.ExpScoreList = new() {
            new GameEndExpScore()
            {
                PlayerId = playerId, Exp = 9999,
            }};
        gameEndMessage.AchievementRecords = new();
        gameEndMessage.TaskRecords = new();
        gameEndMessage.KillCount = (int)(timeStamp % 10000);
        gameEndMessage.GameTime = (int)(timeStamp % 10000);
        gameEndMessage.GameRealTime = (int)(timeStamp % 10000);
        gameEndMessage.GameStartTime = (int)(timeStamp - 10000);
        gameEndMessage.Win = true;
        gameEndMessage.MapUnlockRatio = 1;
        gameEndMessage.Reborn = timeStamp % 2 == 0;
        gameEndMessage.MapType = GameMapType.FixedMap;
        gameEndMessage.TypedMapId = 0;
        gameEndMessage.Difficulty = (int)(timeStamp % 4);
        gameEndMessage.DifficultyLevel = (int)(timeStamp % 10);
        gameEndMessage.MapId = mapList[random.Next() % mapList.Count].id;
        gameEndMessage.ExplorePoint = (int)(timeStamp % 8);
        gameEndMessage.UnlockTileCount = (int)(timeStamp % 1000);
        gameEndMessage.ResourceList = new()
        {
            new GameEndResourceRecord() { Key = "population", AccuCount = random.Next() % 1000, Production = 0},
            new GameEndResourceRecord() { Key = "food", AccuCount = random.Next() % 100, Production = 0},
            new GameEndResourceRecord() { Key = "ink", AccuCount = random.Next() % 1000, Production = random.Next() % 1000},
            new GameEndResourceRecord() { Key = "wood", AccuCount = random.Next() % 1000, Production = random.Next() % 100},
            new GameEndResourceRecord() { Key = "stone", AccuCount = random.Next() % 100, Production = random.Next() % 100},
            new GameEndResourceRecord() { Key = "aura", AccuCount = random.Next() % 100, Production = random.Next() % 10},
            new GameEndResourceRecord() { Key = "production_link", AccuCount = random.Next() % 10, Production = 0},
        };
        gameEndMessage.FightUnitList = new();
        gameEndMessage.HeadquarterHpPercent = 1;
        gameEndMessage.TrainCount = (int)(timeStamp % 100);
        gameEndMessage.DynamicInfo = "{}";
        gameEndMessage.AllBuildingBuiltThroughoutGame = new();

        foreach (var buildingConfig in serverConfigService.GetBuildingConfigList())
        {
            if (random.Next() % 10 > 5) continue;
            int cnt = random.Next() % 100;
            gameEndMessage.BuildingList.Add(new GameEndBuildingInfo()
            {
                PlayerId = playerId,
                Id = buildingConfig.id,
                Count = cnt,
                DamageCount = random.Next() % 10,
            });
            gameEndMessage.AllBuildingBuiltThroughoutGame.Add(buildingConfig.key);
            gameEndMessage.AchievementRecords.Add(new AchievementRecord()
            {
                Key = "build_building",
                Target = buildingConfig.key,
                Count = cnt,
            });
        }
        foreach (var fightConfig in serverConfigService.GetFightCardItemConfigList())
        {
            if (gameEndMessage.FightUnitList.Count >= 5) break;
            if (random.Next() % 10 > 8) continue;
            gameEndMessage.AchievementRecords.Add(new AchievementRecord()
            {
                Key = "soldier_use",
                Target = fightConfig.detailed_key,
                Count = 1,
            });
            gameEndMessage.FightUnitList.Add(new GameEndFightUnitInfo()
            {
                Key = fightConfig.detailed_key,
                Level = random.Next() % 5,
                PlayerId = playerId,
                TotalDamage = random.Next() % 100000
            });
        }
        foreach (var fightConfig in serverConfigService.GetFightCardItemConfigList())
        {
            if (random.Next() % 10 > 8) continue;
            gameEndMessage.AchievementRecords.Add(new AchievementRecord()
            {
                Key = "soldier_use",
                Target = fightConfig.detailed_key,
                Count = 1,
            });
        }
        foreach (var enemyConfig in serverConfigService.GetEnemyConfigList())
        {
            if (random.Next() % 10 > 8) continue;
            gameEndMessage.AchievementRecords.Add(new AchievementRecord()
            {
                Key = "enemy_kill",
                Target = enemyConfig.enemy_group,
                Count = random.Next() % 1000,
            });
        }
        foreach (var buffConfig in serverConfigService.GetFightBuffConfigList())
        {
            if (random.Next() % 10 > 6) continue;
            gameEndMessage.AchievementRecords.Add(new AchievementRecord()
            {
                Key = "game_buff_use",
                Target = buffConfig.achievement_group,
                Count = 1,
            });
        }
        gameEndMessage.AchievementRecords.Add(new AchievementRecord()
        {
            Key = "hero_win_game",
            Target = "qinfeng",
            Count = 1,
        });
        if (taskData != null)
            foreach (var task in taskData.TaskList)
            {
                var taskConfig = serverConfigService.GetBeginnerTaskConfig(task.Id);
                if (taskConfig != null)
                {
                    gameEndMessage.TaskRecords.Add(new TaskRecord()
                    {
                        Key = $"{taskConfig.key}|{taskConfig.target_key}",
                        Count = random.Next() % 10,
                    });
                }
            }
        return gameEndMessage;
    }

    #endregion

    #region 一击必杀unit test

    [HttpGet]
    public async Task<ActionResult<Dictionary<int, int>>> GetOneShotKillVictoryRecord(int activityId)
    {
        var victoryRecord = await activityService.GetOneShotKillVictoryRecordAsync(activityId);
        return Ok(victoryRecord);
    }

    [HttpPost]
    public async Task<ActionResult<Dictionary<int, int>>> SetOneShotKillVictoryRecord(int activityId, string timeString, int level, int val)
    {
        var victoryRecord = await activityService.SetOneShotKillVictoryRecordAsync(
            activityId, timeString, level, val);
        return Ok(victoryRecord);
    }

    [HttpPost]
    public async Task<ActionResult> SetOneShotKillLevelProgress(int activityId, int level, int progress)
    {
        await activityService.SetOneShotKillLevelProgressAsync(
            activityId, level, progress);
        return Ok();
    }

    [HttpPost]
    public async Task<ActionResult> SetOneShotKillTaskProgress(int activityId, int taskId, int progress)
    {
        await activityService.SetOneShotKillTaskProgressAsync(
            activityId, taskId, progress);
        return Ok();
    }

    [HttpPost]
    public async Task<ActionResult> FakeOneShotKillVictory(int activityId, int level, List<int> taskProgressAdded, bool isWin, bool isChallengeMode)
    {
        await activityService.AddOneShotKillVictoryAsync(
            activityId, level, taskProgressAdded, isWin, isChallengeMode);
        return Ok();
    }

    [HttpPost]
    public async Task<ActionResult> ClearOneShotKillServerData(int activityId)
    {
        await activityService.ClearOneShotKillServerData(activityId);
        return Ok();
    }

    #endregion
}