using AutoMapper;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Api.V1.Mappers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Client -> ClientResponseDto
        CreateMap<ClientEntity, ClientResponseDto>()
            .ForMember(dest => dest.Name,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.Hostname : "unknown"))
            .ForMember(dest => dest.IpAddress,
                opt => opt.MapFrom(src =>
                    src.NetworkInfos != null && src.NetworkInfos.Any(n => n.IsActive)
                        ? src.NetworkInfos.First(n => n.IsActive).IpAddress
                        : "unknown"))
            .ForMember(dest => dest.GroupName,
                opt => opt.MapFrom(src =>
                    src.Group != null ? src.Group.Name : null))
            .ForMember(dest => dest.LastSeen,
                opt => opt.MapFrom(src =>
                    src.NetworkInfos != null && src.NetworkInfos.Any(n => n.IsActive)
                        ? src.NetworkInfos.First(n => n.IsActive).LastSeen
                        : (DateTime?)null))
            .ForMember(dest => dest.OsVersion,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.OsVersion : null));

        // Client -> ClientDetailResponseDto
        CreateMap<ClientEntity, ClientDetailResponseDto>()
            .IncludeBase<ClientEntity, ClientResponseDto>()
            .ForMember(dest => dest.UserAgent,
                opt => opt.MapFrom(src =>
                    src.Sessions != null && src.Sessions.Any(s => s.IsActive)
                        ? src.Sessions.First(s => s.IsActive).UserAgent
                        : null))
            .ForMember(dest => dest.CpuInfo,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.CpuInfo : null))
            .ForMember(dest => dest.MemoryGb,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.MemoryGb : null))
            .ForMember(dest => dest.DiskGb,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.DiskGb : null))
            .ForMember(dest => dest.Architecture,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.Architecture : null))
            .ForMember(dest => dest.KernelVersion,
                opt => opt.MapFrom(src =>
                    src.ComputerInfo != null ? src.ComputerInfo.KernelVersion : null))
            .ForMember(dest => dest.BlockedReason,
                opt => opt.MapFrom(src =>
                    src.IsBlocked && src.BlockHistory != null && src.BlockHistory.Any()
                        ? src.BlockHistory.OrderByDescending(b => b.CreatedAt).First().Reason
                        : null))
            .ForMember(dest => dest.BlockedAt,
                opt => opt.MapFrom(src =>
                    src.IsBlocked && src.BlockHistory != null && src.BlockHistory.Any()
                        ? src.BlockHistory.OrderByDescending(b => b.CreatedAt).First().CreatedAt
                        : (DateTime?)null))
            .ForMember(dest => dest.BlockedBy,
                opt => opt.MapFrom(src =>
                    src.IsBlocked && src.BlockHistory != null && src.BlockHistory.Any()
                        ? src.BlockHistory.OrderByDescending(b => b.CreatedAt).First().BlockedBy
                        : null));

        // ClientHistory -> ClientHistoryResponseDto
        CreateMap<ClientHistoryEntity, ClientHistoryResponseDto>()
            .ForMember(dest => dest.ChangeType,
                opt => opt.MapFrom(src => src.ChangeType.ToString()));

        // Group -> GroupResponseDto
        CreateMap<GroupEntity, GroupResponseDto>()
            .ForMember(dest => dest.ClientCount,
                opt => opt.MapFrom(src =>
                    src.Clients != null ? src.Clients.Count(c => c.IsActive) : 0));

        // Group -> GroupDetailResponseDto
        CreateMap<GroupEntity, GroupDetailResponseDto>()
            .IncludeBase<GroupEntity, GroupResponseDto>();

        // User -> UserResponseDto
        CreateMap<UserEntity, UserResponseDto>();

        // UserClientAccess -> UserClientAccessDto
        CreateMap<UserClientAccessEntity, UserClientAccessDto>()
            .ForMember(dest => dest.ClientName,
                opt => opt.MapFrom(src =>
                    src.Client != null && src.Client.ComputerInfo != null
                        ? src.Client.ComputerInfo.Hostname
                        : src.ClientId));

        // UserGroupAccess -> UserGroupAccessDto
        CreateMap<UserGroupAccessEntity, UserGroupAccessDto>()
            .ForMember(dest => dest.GroupName,
                opt => opt.MapFrom(src =>
                    src.Group != null ? src.Group.Name : src.GroupId));
    }
}