using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Repositories
{
    /// <summary>
    /// Abstracción del patrón Unit of Work y manejo transaccional ACID
    /// para coordinar operaciones de escritura atómicas entre múltiples repositorios
    /// sin acoplar la capa de BusinessLogic a Entity Framework Core o AppDbContext.
    /// </summary>
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    }
}

