using Ecosphere.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Infrastructure.Infrastructure.Persistence;

public class EcosphereDbContext : IdentityDbContext<EcosphereUser, ApplicationRole, long>
{
    public EcosphereDbContext(DbContextOptions<EcosphereDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<ContactRequest> ContactRequests { get; set; }
    public DbSet<Call> Calls { get; set; }
    public DbSet<CallParticipant> CallParticipants { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<MeetingParticipant> MeetingParticipants { get; set; }
    public DbSet<MeetingJoinRequest> MeetingJoinRequests { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<TurnCredentials> TurnCredentials { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Device>()
            .HasIndex(d => new { d.UserId, d.DeviceToken })
            .IsUnique();

        builder.Entity<Contact>()
            .HasIndex(c => new { c.UserId, c.ContactUserId })
            .IsUnique();

        builder.Entity<ContactRequest>()
            .HasIndex(cr => new { cr.SenderId, cr.ReceiverId })
            .IsUnique();

        builder.Entity<Meeting>()
            .HasIndex(m => m.MeetingCode)
            .IsUnique();

        builder.Entity<Call>()
            .HasIndex(c => c.CallUuid)
            .IsUnique();

        // Configure foreign keys (without navigation properties)
        builder.Entity<Device>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Contact>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Contact>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(c => c.ContactUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ContactRequest>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(cr => cr.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ContactRequest>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(cr => cr.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Meeting>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(m => m.HostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MeetingParticipant>()
            .HasOne<Meeting>()
            .WithMany()
            .HasForeignKey(mp => mp.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MeetingParticipant>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(mp => mp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MeetingJoinRequest>()
            .HasOne<Meeting>()
            .WithMany()
            .HasForeignKey(mjr => mjr.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MeetingJoinRequest>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(mjr => mjr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Call>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(c => c.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CallParticipant>()
            .HasOne<Call>()
            .WithMany()
            .HasForeignKey(cp => cp.CallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CallParticipant>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(cp => cp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CallParticipant>()
            .HasOne<Device>()
            .WithMany()
            .HasForeignKey(cp => cp.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<RefreshToken>()
            .HasOne<EcosphereUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasOne(m => m.Meeting)
            .WithMany()
            .HasForeignKey(m => m.MeetingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Message>()
            .HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt });

        builder.Entity<Message>()
            .HasIndex(m => new { m.MeetingId, m.SentAt });

        builder.Seed();
    }
}
