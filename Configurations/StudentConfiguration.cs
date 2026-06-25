using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(student => student.Id);

        builder.Property(student => student.RegistrationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(student => student.RegistrationNumber)
            .IsUnique();

        builder.Property(student => student.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(student => student.GPA)
            .HasPrecision(3, 2);

        builder.Property(student => student.IsActive)
            .IsRequired();

        builder.Property<DateTime>("LastUpdated")
            .IsRequired();
        
        builder.Property(student => student.Version)
            .IsRowVersion();
    }
}