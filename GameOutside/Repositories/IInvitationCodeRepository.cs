using GameOutside.DBContext;
using GameOutside.Models;

namespace GameOutside.Repositories;

public interface IInvitationCodeRepository
{
    public Task<bool> IsInvitationCodeClaimed(string invitationCode);
    public void MarkInvitationCodeClaimed(string invitationCode);
}

public class InvitationCodeRepository(BuildingGameDB dbCtx) : IInvitationCodeRepository
{
    [Obsolete]
    public async Task<bool> IsInvitationCodeClaimed(string invitationCode)
    {
        return await dbCtx.InvitationCodeClaimRecords.FindAsync(invitationCode) != null;
    }

    [Obsolete]
    public void MarkInvitationCodeClaimed(string invitationCode)
    {
        dbCtx.InvitationCodeClaimRecords.Add(new InvitationCodeClaimRecord() {GiftCode = invitationCode});
    }
}