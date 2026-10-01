using Finvex.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finvex.Infrastructure;

public sealed class FinvexDbContext(DbContextOptions<FinvexDbContext> options) : DbContext(options)
{
    public DbSet<Tienda> Tiendas => Set<Tienda>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<Cronograma> Cronogramas => Set<Cronograma>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<AdministradorSistema> AdministradoresSistema => Set<AdministradorSistema>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ListadoPago> ListadosPago => Set<ListadoPago>();
    public DbSet<ItemListadoPago> ItemsListadoPago => Set<ItemListadoPago>();
    public DbSet<Operacion> Operaciones => Set<Operacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tienda>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Ruc).HasMaxLength(11).IsRequired();
            entity.Property(x => x.RazonSocial).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Giro).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Usuario).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Usuario).IsUnique();
            entity.HasIndex(x => x.Ruc).IsUnique();
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Dni).HasMaxLength(8).IsRequired();
            entity.Property(x => x.NombresCompletos).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Usuario).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.LimiteCredito).HasPrecision(18, 2);
            entity.Property(x => x.TasaCompensatoria).HasPrecision(18, 7);
            entity.Property(x => x.TasaMoratoria).HasPrecision(18, 7);
            entity.HasIndex(x => new { x.TiendaId, x.Dni }).IsUnique();
            entity.HasIndex(x => new { x.TiendaId, x.Usuario }).IsUnique();
            entity.HasOne<Tienda>().WithMany(x => x.Clientes).HasForeignKey(x => x.TiendaId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("Clientes", table => table.HasCheckConstraint("CK_Cliente_DiaCorte", "DiaCorte BETWEEN 1 AND 28"));
            entity.ToTable("Clientes", table => table.HasCheckConstraint("CK_Cliente_DiaPago", "DiaPago BETWEEN 1 AND 28"));
            entity.ToTable("Clientes", table => table.HasCheckConstraint("CK_Cliente_MaxMeses", "MaxMeses BETWEEN 1 AND 36"));
        });

        modelBuilder.Entity<Compra>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Producto).HasMaxLength(250).IsRequired();
            entity.Property(x => x.PrecioCredito).HasPrecision(18, 2);
            entity.Property(x => x.SaldoCapital).HasPrecision(18, 2);
            entity.HasOne(x => x.Cliente).WithMany(x => x.Compras).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cronograma>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SaldoInicial).HasPrecision(18, 2);
            entity.Property(x => x.Interes).HasPrecision(18, 2);
            entity.Property(x => x.Amortizacion).HasPrecision(18, 2);
            entity.Property(x => x.CuotaFija).HasPrecision(18, 2);
            entity.HasOne(x => x.Compra).WithMany(x => x.Cronogramas).HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CompraId, x.NroCuota }).IsUnique();
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MontoAbonado).HasPrecision(18, 2);
            entity.Property(x => x.ImputacionMora).HasPrecision(18, 2);
            entity.Property(x => x.ImputacionInteres).HasPrecision(18, 2);
            entity.Property(x => x.ImputacionCapital).HasPrecision(18, 2);
            entity.HasOne(x => x.Cliente).WithMany(x => x.Pagos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdministradorSistema>(entity =>
        {
            entity.ToTable("AdministradoresSistema");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Usuario).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Usuario).IsUnique();
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Proveedor).HasMaxLength(150);
            entity.Property(x => x.Marca).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(250).IsRequired();
            entity.Property(x => x.UnidadMedida).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ImagenUrl).HasMaxLength(500);
            entity.Property(x => x.PrecioContado).HasPrecision(18, 2);
            entity.Property(x => x.PrecioLista).HasPrecision(18, 2);
            entity.HasOne<Tienda>().WithMany().HasForeignKey(x => x.TiendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TiendaId, x.Activo });
        });

        modelBuilder.Entity<ListadoPago>(entity =>
        {
            entity.ToTable("ListadosPago");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ClienteId, x.FechaCorte }).IsUnique();
        });

        modelBuilder.Entity<ItemListadoPago>(entity =>
        {
            entity.ToTable("ItemsListadoPago");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Descripcion).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Capital).HasPrecision(18, 2);
            entity.Property(x => x.InteresCompensatorio).HasPrecision(18, 2);
            entity.Property(x => x.Monto).HasPrecision(18, 2);
            entity.HasOne(x => x.ListadoPago).WithMany(x => x.Items).HasForeignKey(x => x.ListadoPagoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Operacion>(entity =>
        {
            entity.ToTable("Operaciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorRol).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Accion).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Entidad).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Detalle).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TiendaId, x.FechaUtc });
            entity.HasIndex(x => x.FechaUtc);
        });
    }
}
