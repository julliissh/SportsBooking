namespace AuthService.Repositories;

// Базовий репозиторій — загальні CRUD операції
public interface IBaseRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task AddAsync(T entity);
    void Remove(T entity);
    Task SaveChangesAsync();
}
