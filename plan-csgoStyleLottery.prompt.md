﻿## Plan: Csgo-Style Lottery Commercial Activity

Introduce Csgo-style lottery endpoints and shared task infrastructure by mirroring Unrivaled God draw/cost logic and Slot Machine reward handling while reusing `ActivityCsgoStyleLottery` storage and Csgo configs. Cover draw, point reward, task reward, pass reward flows, and refactor task tracking so both activities share validation, refresh, and reward routines without duplicating logic.

### Steps
1. [x] Extend `ActivityService`/`IActivityRepository` with CRUD helpers for `ActivityCsgoStyleLottery`, record refresh utilities, and shared task-record methods returning standardized DTOs.
2. [x] Implement Csgo draw flow in `ActivityController.CsgoStyleLottery.cs`: validate activity via `ActivityType`, load configs (`ServerConfigService` getters), handle key/diamond costs, randomize reward, update points/records, persist, and return reward similar to Unrivaled draw reply.
3. [x] Add point reward and task reward endpoints in `ActivityController.CsgoStyleLottery.cs` mirroring Slot Machine and Unrivaled patterns, consuming shared task helpers, updating claim bitmasks, and issuing rewards through `userItemService`.
4. [x] Design pass activation and reward claims in `ActivityController.CsgoStyleLottery.cs`: verify IAP status, track `ActivityPremiumPassStatus`, manage `PremiumPassDailyRewardClaimStatus`, and issue configured rewards; include helper utilities inside service layer for state transitions.
5. [x] Refactor task logic to use unified `AddTaskProgressToActiveActivityTask` method that updates both Unrivaled God and CsgoStyleLottery tasks simultaneously. Removed redundant methods: `AddExplorePointAsync`, `OnPlayEndlessChallengeModeAsync`, `OnPlayRpgGameMode`, `OnPlayCoopBossModeAsync`, `OnPlayTreasureMazeModeAsync`, `OnPlayOneShotKillModeAsync`.

### Implemented Files
- `GameOutside/Util/DataUtil.cs` - Added `CsgoStyleLotteryDataClient` DTO and `DefaultCsgoStyleLotteryData`/`ToClientApi` helpers
- `GameOutside/Util/ActivityType.cs` - Added `ActivityCsgoStyleLottery` constant
- `GameOutside/Repositories/IActivityRepository.cs` - Added interface methods and implementation for CsgoStyleLottery CRUD
- `GameOutside/Services/ActivityService.cs` - Added CsgoStyleLottery service methods: Get, Create, CheckRefresh, RecordTask, RandomizeReward
- `GameOutside/Services/ServerConfigService.cs` - Added `GetCsgoLotteryTaskConfigByKey` helper
- `GameOutside/Controllers/Activity/ActivityController.cs` - Added `CsgoStyleLottery` to `OpeningActivityData` and switch case
- `GameOutside/Controllers/Activity/ActivityController.CsgoStyleLottery.cs` - Full implementation of all endpoints

### Implemented Endpoints
1. `DrawCsgoStyleLottery` - 抽奖接口，消耗钥匙或玉璧
2. `ClaimCsgoStyleLotteryPointReward` - 领取积分奖励
3. `ClaimCsgoStyleLotteryTaskReward` - 领取任务奖励
4. `PurchaseCsgoStyleLotteryKey` - 购买钥匙
5. `ActivateCsgoStyleLotteryPremiumPass` - 激活高级通行证
6. `ClaimCsgoStyleLotteryPassDailyReward` - 领取通行证每日奖励
7. `GetCsgoStyleLotteryData` - 获取活动数据

### Config Gaps (marked with `//csgo lottery todo`)
1. **抽奖消耗配置**: `diamondCostPerDraw` - 需要从配置表获取单次抽奖消耗的玉璧数量
2. **保底机制配置**: `guaranteeCount`, `guaranteeQuality` - 需要配置表定义保底机制参数
3. **钥匙购买配置**: `keyPricePerUnit`, `maxKeyPurchaseCount` - 需要配置表定义钥匙购买价格和购买限制
4. **任务key常量**: 需要定义任务key常量（如 "draw_lottery", "login_daily"）
5. **IAP验证**: 通行证激活需要验证IAP购买状态
6. **专用错误码**: 可以为CsgoStyleLottery定义专用错误码

### Further Considerations
1. Confirm draw cost priority (keys vs diamonds) and whether pity/reward record logic must match existing Csgo spec or Unrivaled behavior.
2. Clarify Csgo pass lifecycle: is activation purely IAP flag, and do daily rewards reset by timezone or server UTC?
3. Consider adding database migration for `ActivityCsgoStyleLottery` table if not already present.

