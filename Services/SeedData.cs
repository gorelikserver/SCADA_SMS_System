using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace SCADASMSSystem.Web.Services
{
    public static class SeedData
    {
        public static async Task InitializeAsync(SCADADbContext context, ILogger logger)
        {
            try
            {
                logger.LogInformation("Starting database seeding...");

                // Check if data already exists
                if (await context.Users.AnyAsync())
                {
                    logger.LogInformation("Database already contains data, skipping seeding");
                    return;
                }

                // Seed Users
                var users = new[]
                {
                    new User
                    {
                        UserName = "System Administrator",
                        Email = "admin@scada.local",
                        PhoneNumber = "+1-555-0001",
                        SmsEnabled = true,
                        SpecialDaysEnabled = true,
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        UserName = "Control Room Operator",
                        Email = "operator@scada.local",
                        PhoneNumber = "+1-555-0002",
                        SmsEnabled = true,
                        SpecialDaysEnabled = true,
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        UserName = "Maintenance Supervisor",
                        Email = "maintenance@scada.local",
                        PhoneNumber = "+1-555-0003",
                        SmsEnabled = true,
                        SpecialDaysEnabled = false,
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        UserName = "Field Technician",
                        Email = "field.tech@scada.local",
                        PhoneNumber = "+1-555-0004",
                        SmsEnabled = true,
                        SpecialDaysEnabled = false,
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        UserName = "Emergency Manager",
                        Email = "emergency@scada.local",
                        PhoneNumber = "+1-555-0005",
                        SmsEnabled = true,
                        SpecialDaysEnabled = true,
                        CreatedAt = DateTime.Now
                    }
                };

                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} users", users.Length);

                // Seed Groups
                var groups = new[]
                {
                    new Group
                    {
                        GroupName = "Critical Alarms",
                        CreatedAt = DateTime.Now
                    },
                    new Group
                    {
                        GroupName = "Maintenance Team",
                        CreatedAt = DateTime.Now
                    },
                    new Group
                    {
                        GroupName = "Emergency Response",
                        CreatedAt = DateTime.Now
                    },
                    new Group
                    {
                        GroupName = "Management",
                        CreatedAt = DateTime.Now
                    }
                };

                await context.Groups.AddRangeAsync(groups);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} groups", groups.Length);

                // Seed Group Members
                var groupMembers = new[]
                {
                    // Critical Alarms Group - All users
                    new GroupMember { GroupId = 1, UserId = 1, CreatedAt = DateTime.Now },
                    new GroupMember { GroupId = 1, UserId = 2, CreatedAt = DateTime.Now },
                    new GroupMember { GroupId = 1, UserId = 5, CreatedAt = DateTime.Now },

                    // Maintenance Team
                    new GroupMember { GroupId = 2, UserId = 3, CreatedAt = DateTime.Now },
                    new GroupMember { GroupId = 2, UserId = 4, CreatedAt = DateTime.Now },

                    // Emergency Response
                    new GroupMember { GroupId = 3, UserId = 1, CreatedAt = DateTime.Now },
                    new GroupMember { GroupId = 3, UserId = 2, CreatedAt = DateTime.Now },
                    new GroupMember { GroupId = 3, UserId = 5, CreatedAt = DateTime.Now },

                    // Management
                    new GroupMember { GroupId = 4, UserId = 1, CreatedAt = DateTime.Now },
                    new GroupMember { GroupId = 4, UserId = 5, CreatedAt = DateTime.Now }
                };

                await context.GroupMembers.AddRangeAsync(groupMembers);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} group memberships", groupMembers.Length);

                // Seed some sample audit records
                var sampleAudits = new[]
                {
                    new SmsAudit
                    {
                        AlarmId = "INIT_001",
                        UserId = 1,
                        PhoneNumber = "+1-555-0001",
                        AlarmDescription = "System initialization complete",
                        Status = "SUCCESS",
                        MessageStatus = "Delivered",
                        ApiResponse = "SMS sent successfully",
                        CreatedAt = DateTime.Now.AddMinutes(-5)
                    },
                    new SmsAudit
                    {
                        AlarmId = "INIT_002",
                        UserId = 2,
                        PhoneNumber = "+1-555-0002",
                        AlarmDescription = "Database seeding notification",
                        Status = "SUCCESS",
                        MessageStatus = "Delivered",
                        ApiResponse = "SMS sent successfully",
                        CreatedAt = DateTime.Now.AddMinutes(-2)
                    }
                };

                await context.SmsAudits.AddRangeAsync(sampleAudits);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} sample audit records", sampleAudits.Length);

                logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during database seeding");
                throw;
            }
        }
    }
}