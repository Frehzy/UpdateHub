using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UpdateHub.BackendServer.Infrastructure.Database;

#nullable disable

namespace UpdateHub.BackendServer.Migrations;

/// <summary>
/// Снимок модели после последней применённой миграции.
/// </summary>
/// <remarks>
/// По этому файлу инструменты вычисляют, что изменилось в модели с прошлого
/// раза, и формируют текст следующей миграции. Он же позволяет проверить
/// согласованность прямо из тестов: <c>Database.HasPendingModelChanges()</c>
/// сравнивает снимок с текущей моделью и возвращает <c>true</c>, если миграции
/// отстали от сущностей.
/// </remarks>
[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    /// <inheritdoc />
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        InitialModel.Build(modelBuilder);
    }
}
