using System.ComponentModel.DataAnnotations.Schema;
using ChillyRoom.Functions.DBModel;

namespace AssistActivity.Models
{
    public enum AssistLevel
    {
        OutOfRange = 0, // 无效等级
        Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4
    }

    public class PlayerAssistActivityInfo : BaseEntity
    {
        public long PlayerId { get; set; }
        public short ShardId { get; set; }
        public Guid DistroId { get; set; }

        public string ActivityName { get; set; }

        // JSONB 存储活动特定数据
        [Column(TypeName = "jsonb")]
        public AssistActivityPayload Payload { get; set; } = new();

        public class AssistActivityPayload
        {
            public string InviteCode { get; set; }

            public string[] HistoryInviteCodes { get; set; } = Array.Empty<string>();

            // 邀请码领取记录（pid）
            public Dictionary<int, HashSet<long>> InviteCodeRedeemedPlayerIds { get; set; } = new();

            // 安全访问器
            public IEnumerable<string> GetHistoryInviteCodesOrEmpty()
                => HistoryInviteCodes ?? Array.Empty<string>();

            // 助力相关（可复用于其他社交活动）
            public int BeAssistedCount { get; set; } = 0;
            public List<string> AssistFromUniqueIdentifier { get; set; } = new();
        }

        public int GetPlayerBeAssistedCount() => Payload.BeAssistedCount;

        public bool HasAssistedFromUniqueIdentifier(string uniqueIdentifier) =>
            Payload.AssistFromUniqueIdentifier.Contains(uniqueIdentifier);
    }

    public class AssistActivityConfig
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int MaxInviteAssists { get; set; } = 3; // 每人被助力次数上限
        public Guid RewardMailTemplate { get; set; }
        public Dictionary<string, string> GameDownloadUrls { get; set; } = new Dictionary<string, string>();
        public string InviteCodePrefix { get; set; } = "ink-";

        public string ShareLink { get; set; } = "https://pages.test.chilly.tech/GameMarketingApp/#/game/inkvasion";

        // 邀请码被领取数限制
        public int MaxInviteCodeRedemptions { get; set; } = 3;
    }

    /// <summary>
    /// 活动相关错误码枚举
    /// </summary>
    public enum ActivityErrorCode : int
    {
        // 活动状态相关错误 (30001-30010)
        ACTIVITY_NOT_ACTIVE = 30001,
        ACTIVITY_NOT_FOUND = 30002,

        // 玩家相关错误 (30011-30020)
        PLAYER_ACTIVITY_INFO_NOT_FOUND = 30011,
        PLAYER_NOT_FOUND = 30012,

        // 邀请码相关错误 (30021-30030)
        INVITE_CODE_EMPTY = 30021,
        INVITE_CODE_INVALID_FORMAT = 30022,
        INVITE_CODE_PARSE_FAILED = 30023,
        INVITE_CODE_ACTIVITY_MISMATCH = 30024,
        INVITE_CODE_DISTRO_MISMATCH = 30025,

        // 邀请码已过期
        INVITE_CODE_EXPIRED = 30026,

        // 助力相关错误 (30031-30040)
        ASSIST_LIMIT_REACHED = 30031,
        ASSIST_TARGET_LIMIT_REACHED = 30032,
        ASSIST_ALREADY_ASSISTED = 30033,
        ASSIST_SELF_NOT_ALLOWED = 30034,

        // 进度推进相关错误 (30041-30050)
        PROGRESS_INVALID_RUB_COUNT = 30041,
        PROGRESS_NO_REMAINING_COUNT = 30042,

        // 奖励相关错误 (30051-30060)
        REWARD_TEMPLATE_NOT_FOUND = 30051,

        // 重置相关错误 (30061-30070)
        RESET_NOT_ELIGIBLE = 30061,
    }
}