using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Configurations;

public class EnrollmentConfiguration
    : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(enrollment => enrollment.Id);

        builder.Property(enrollment => enrollment.StudentId)
            .IsRequired();

        builder.Property(enrollment => enrollment.CourseId)
            .IsRequired();

        builder.Property(enrollment => enrollment.Grade)
            .HasPrecision(3, 2);

        builder.Property(enrollment => enrollment.EnrolledAt)
            .IsRequired();

        builder.HasOne(enrollment => enrollment.Student)
            .WithMany(student => student.Enrollments)
            .HasForeignKey(enrollment => enrollment.StudentId);

        builder.HasOne(enrollment => enrollment.Course)
            .WithMany(course => course.Enrollments)
            .HasForeignKey(enrollment => enrollment.CourseId);
    }
}