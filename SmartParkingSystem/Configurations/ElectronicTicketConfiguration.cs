using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Models;

namespace SmartParking.Configurations
{
    public class ElectronicTicketConfiguration : IEntityTypeConfiguration<ElectronicTicket>
    {
        public void Configure(EntityTypeBuilder<ElectronicTicket> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TicketCode)
                .HasMaxLength(50)
                .IsRequired();

            builder.HasIndex(x => x.TicketCode)
                .IsUnique();

            builder.HasIndex(x => x.BookingId)
                .IsUnique();

            builder.HasOne(x => x.Booking)
                .WithOne(x => x.ElectronicTicket)
                .HasForeignKey<ElectronicTicket>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
