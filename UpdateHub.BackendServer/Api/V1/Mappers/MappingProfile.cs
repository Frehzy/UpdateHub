using AutoMapper;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Enrollments;
using UpdateHub.BackendServer.Domain.Entities.Groups;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.Shared.Contracts.Clients;
using UpdateHub.Shared.Contracts.Enrollments;
using UpdateHub.Shared.Contracts.Groups;
using UpdateHub.Shared.Contracts.Users;

namespace UpdateHub.BackendServer.Api.V1.Mappers;

/// <summary>
/// Правила преобразования сущностей в модели ответа панели управления.
/// </summary>
/// <remarks>
/// Клиентская часть API преобразованиями не пользуется: она отдаёт текст,
/// который собирается вручную в контроллерах.
/// </remarks>
public class MappingProfile : Profile
{
    /// <summary>Настраивает все преобразования.</summary>
    public MappingProfile()
    {
        MapClients();
        MapGroups();
        MapUsers();
        MapEnrollments();
    }

    /// <summary>Настраивает преобразования компьютеров.</summary>
    private void MapClients()
    {
        CreateMap<ClientEntity, ClientResponseDto>()
            .ForMember(d => d.Name, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.Hostname : "не указано"))
            .ForMember(d => d.OsVersion, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.OsVersion : null))
            .ForMember(d => d.GroupName, o => o.MapFrom(s =>
                s.Group != null ? s.Group.Name : null))
            .ForMember(d => d.IpAddress, o => o.MapFrom(s =>
                s.NetworkInfos.Where(n => n.IsActive)
                    .OrderByDescending(n => n.LastSeen)
                    .Select(n => n.IpAddress)
                    .FirstOrDefault()))
            .ForMember(d => d.LastSeen, o => o.MapFrom(s =>
                s.NetworkInfos.OrderByDescending(n => n.LastSeen)
                    .Select(n => (DateTime?)n.LastSeen)
                    .FirstOrDefault()));

        CreateMap<ClientEntity, ClientDetailResponseDto>()
            .IncludeBase<ClientEntity, ClientResponseDto>()
            .ForMember(d => d.HardwareFingerprint, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.HardwareFingerprint : null))
            .ForMember(d => d.CpuInfo, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.CpuInfo : null))
            .ForMember(d => d.MemoryGb, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.MemoryGb : null))
            .ForMember(d => d.DiskGb, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.DiskGb : null))
            .ForMember(d => d.Architecture, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.Architecture : null))
            .ForMember(d => d.KernelVersion, o => o.MapFrom(s =>
                s.ComputerInfo != null ? s.ComputerInfo.KernelVersion : null))
            .ForMember(d => d.BlockedReason, o => o.MapFrom(s =>
                s.IsBlocked
                    ? s.BlockHistory.Where(b => b.Action == "blocked")
                        .OrderByDescending(b => b.CreatedAt)
                        .Select(b => b.Reason)
                        .FirstOrDefault()
                    : null))
            .ForMember(d => d.BlockedAt, o => o.MapFrom(s =>
                s.IsBlocked
                    ? s.BlockHistory.Where(b => b.Action == "blocked")
                        .OrderByDescending(b => b.CreatedAt)
                        .Select(b => (DateTime?)b.CreatedAt)
                        .FirstOrDefault()
                    : null))
            .ForMember(d => d.BlockedBy, o => o.MapFrom(s =>
                s.IsBlocked
                    ? s.BlockHistory.Where(b => b.Action == "blocked")
                        .OrderByDescending(b => b.CreatedAt)
                        .Select(b => b.BlockedBy)
                        .FirstOrDefault()
                    : null))
            .ForMember(d => d.History, o => o.Ignore());

        CreateMap<ClientHistoryEntity, ClientHistoryResponseDto>()
            .ForMember(d => d.ChangeType, o => o.MapFrom(s => s.ChangeType.ToString()));
    }

    /// <summary>Настраивает преобразования групп.</summary>
    private void MapGroups()
    {
        CreateMap<GroupEntity, GroupResponseDto>()
            .ForMember(d => d.ClientCount, o => o.MapFrom(s => s.Clients.Count(c => c.IsActive)));

        CreateMap<GroupEntity, GroupDetailResponseDto>()
            .IncludeBase<GroupEntity, GroupResponseDto>()
            .ForMember(d => d.Clients, o => o.Ignore());
    }

    /// <summary>Настраивает преобразования пользователей и выданных им прав.</summary>
    private void MapUsers()
    {
        CreateMap<UserEntity, UserResponseDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.ClientAccesses, o => o.MapFrom(s => s.UserClientAccesses))
            .ForMember(d => d.GroupAccesses, o => o.MapFrom(s => s.UserGroupAccesses));

        CreateMap<UserClientAccessEntity, UserClientAccessDto>()
            .ForMember(d => d.ClientName, o => o.MapFrom(s =>
                s.Client != null && s.Client.ComputerInfo != null
                    ? s.Client.ComputerInfo.Hostname
                    : s.ClientId));

        CreateMap<UserGroupAccessEntity, UserGroupAccessDto>()
            .ForMember(d => d.GroupName, o => o.MapFrom(s => s.Group != null ? s.Group.Name : s.GroupId));
    }

    /// <summary>Настраивает преобразования заявок на регистрацию.</summary>
    private void MapEnrollments()
    {
        CreateMap<EnrollmentRequestEntity, EnrollmentResponseDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.MatchingClientIds, o => o.Ignore());
    }
}
