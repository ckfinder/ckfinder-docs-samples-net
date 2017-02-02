namespace FileSystem.Database
{
    using System.Data.Entity;

    public class Context : DbContext
    {
        public Context(string connectionString)
            : base(connectionString)
        {
        }

        public DbSet<DatabaseNode> Nodes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //modelBuilder.Entity<KeyValue>().ToTable($"{_tablePrefix}KeyValues").HasKey(x => new { x.Key, x.Filter });
        }
    }
}