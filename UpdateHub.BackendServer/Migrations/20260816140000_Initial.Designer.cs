using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UpdateHub.BackendServer.Infrastructure.Database;

#nullable disable

namespace UpdateHub.BackendServer.Migrations;

/// <summary>
/// Модель на момент применения миграции <see cref="Initial"/>.
/// </summary>
/// <remarks>
/// Атрибут <c>Migration</c> задаёт идентификатор миграции: именно эта строка
/// попадает в таблицу <c>__EFMigrationsHistory</c> и по ней сервер понимает,
/// что миграция уже применена.
/// </remarks>
[DbContext(typeof(AppDbContext))]
[Migration("20260816140000_Initial")]
partial class Initial
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        InitialModel.Build(modelBuilder);
    }
}
