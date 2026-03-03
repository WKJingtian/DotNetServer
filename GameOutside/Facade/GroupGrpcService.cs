
using ChillyRoom.BuildingGame.v1;
using GameOutside.Repositories;
using Grpc.Core;

namespace GameOutside.Facade;

public class GroupService(IUserRankGroupRepository groupRepository) : Group.GroupBase
{
    public override async Task<GetGroupIdResponse> GetGroupId(GetGroupIdRequest request, ServerCallContext context)
    {
        return new GetGroupIdResponse
        {
            GroupId = await groupRepository.GetLocalGroupIdAsync(request.SeasonNumber, request.DivisionNumber)
        };
    }
}