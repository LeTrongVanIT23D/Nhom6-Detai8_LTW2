using Microsoft.EntityFrameworkCore;
using StudentManagement.Models;

namespace StudentManagement.Data;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DoctorLeaveRequest> DoctorLeaveRequests => Set<DoctorLeaveRequest>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<OtcMedicine> OtcMedicines => Set<OtcMedicine>();
    public DbSet<OtcOrder> OtcOrders => Set<OtcOrder>();
    public DbSet<OtcOrderItem> OtcOrderItems => Set<OtcOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.Property(doctor => doctor.ProfileImageUrl).HasMaxLength(500);
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Phone).HasMaxLength(30);
            entity.HasOne<Doctor>()
                .WithMany()
                .HasForeignKey(user => user.DoctorId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(user => user.DoctorId)
                .IsUnique()
                .HasFilter("[DoctorId] IS NOT NULL");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(a => a.BookingSource)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)")
                .HasDefaultValue(AppointmentBookingSource.InPerson);
            entity.Property(a => a.BookingFee).HasPrecision(18, 2);
            entity.Property(a => a.DiscountAmount).HasPrecision(18, 2);
            entity.Property(a => a.BookingFeePaymentMethod).HasMaxLength(30);
            entity.Property(a => a.BookingFeePaymentReference).HasMaxLength(100);

            entity.Property(a => a.Status)
                .HasConversion<string>();

            entity.HasOne(a => a.Patient)
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Doctor)
                .WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Receptionist)
                .WithMany()
                .HasForeignKey(a => a.ReceptionistId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SupportMessage>()
            .Property(s => s.Status)
            .HasConversion<string>();

        modelBuilder.Entity<DoctorLeaveRequest>()
            .Property(d => d.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.SupportUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasOne<ChatRoom>()
                .WithMany()
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasOne(r => r.Doctor)
                .WithMany()
                .HasForeignKey(r => r.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Patient)
                .WithMany()
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.Appointment)
                .WithMany()
                .HasForeignKey(r => r.AppointmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<OtcOrder>(entity =>
        {
            entity.Property(order => order.FulfillmentMethod).HasConversion<string>().HasMaxLength(20);
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(order => order.PaymentMethod).HasMaxLength(30);
            entity.Property(order => order.PaymentReference).HasMaxLength(100);
            entity.Property(order => order.ReceiverName).HasMaxLength(200);
            entity.Property(order => order.ReceiverPhone).HasMaxLength(30);
            entity.Property(order => order.DeliveryAddress).HasMaxLength(500);
            entity.Property(order => order.SubTotal).HasPrecision(18, 2);
            entity.Property(order => order.DiscountAmount).HasPrecision(18, 2);
            entity.Property(order => order.FinalAmount).HasPrecision(18, 2);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(order => order.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OtcOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OtcMedicine>(entity =>
        {
            entity.Property(medicine => medicine.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<OtcOrderItem>(entity =>
        {
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.LineTotal).HasPrecision(18, 2);
            entity.HasOne<OtcMedicine>()
                .WithMany()
                .HasForeignKey(item => item.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>()
            .Property(invoice => invoice.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OtcMedicine>()
            .Property(medicine => medicine.Price)
            .HasPrecision(18, 2);
    }
}
