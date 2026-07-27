using System.Linq.Expressions;

namespace TrainingCenter.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();//get all is IEnumurible , take no parametars,take time
        Task<T?> GetByIdAsync(int id);//take time
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);//take time and we use expression on it get list
        Task AddAsync(T entity);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        void Update(T entity);//no take time its exucute when save 
        void Delete(T entity);

    }
}