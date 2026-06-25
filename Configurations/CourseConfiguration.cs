using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(course => course.Id);

        builder.Property(course => course.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(course => course.Code)
            .IsUnique();

        builder.Property(course => course.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(course => course.Capacity)
            .IsRequired();
    }
}