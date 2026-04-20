using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Models;

namespace SmartParking.Configurations
{
    public class CheckInOutConfiguration : IEntityTypeConfiguration<CheckInOut>
    {
        public void Configure(EntityTypeBuilder<CheckInOut> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.LicensePlate)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(c => c.PlateImagePath)
                .HasMaxLength(500);

            builder.Property(c => c.CheckInStationId)
                .HasMaxLength(50);

            builder.Property(c => c.CheckOutStationId)
                .HasMaxLength(50);

            builder.Property(c => c.FeeStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            builder.Property(c => c.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");

            builder.Property(c => c.FeeAmount)
                .HasPrecision(10, 2);

            // Indexes for performance
            builder.HasIndex(c => new { c.LicensePlate, c.Status })
                .HasName("IX_LicensePlate_Active")
                .HasFilter("[Status] = 'Active'");

            builder.HasIndex(c => c.CheckInTime)
                .HasName("IX_CheckInTime");

            builder.HasIndex(c => c.FeeStatus)
                .HasName("IX_FeeStatus");
        }
    }
}
