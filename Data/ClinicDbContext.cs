using Microsoft.EntityFrameworkCore;
using StudentManagement.Models;

namespace StudentManagement.Data;

public class ClinicDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Specialty> Specialties => Set<Specialty>();

    public ClinicDbContext(DbContextOptions<ClinicDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<Patient>()
            .HasIndex(patient => patient.UserId)
            .IsUnique();

        modelBuilder.Entity<Doctor>()
            .HasIndex(doctor => doctor.UserId)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(user => user.PatientProfile)
            .WithOne(patient => patient.User)
            .HasForeignKey<Patient>(patient => patient.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(user => user.DoctorProfile)
            .WithOne(doctor => doctor.User)
            .HasForeignKey<Doctor>(doctor => doctor.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Specialty>()
            .HasMany(specialty => specialty.Doctors)
            .WithOne(doctor => doctor.SpecialtyEntity)
            .HasForeignKey(doctor => doctor.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Doctor>()
            .Ignore(doctor => doctor.Specialty);

        modelBuilder.Entity<Specialty>().HasData(
            new Specialty { Id = 1, Name = "Nội khoa", Description = "Khám và điều trị các bệnh lý nội khoa." },
            new Specialty { Id = 2, Name = "Nhi khoa", Description = "Chăm sóc sức khỏe trẻ em." },
            new Specialty { Id = 3, Name = "Răng hàm mặt", Description = "Khám và điều trị các bệnh lý răng hàm mặt." },
            new Specialty { Id = 4, Name = "Tim mạch", Description = "Khám và theo dõi sức khỏe tim mạch." },
            new Specialty { Id = 5, Name = "Da liễu", Description = "Khám và điều trị các bệnh lý da liễu." });
    }
}