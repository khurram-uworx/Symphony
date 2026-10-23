using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Symphony.Data;

public sealed class SymphonyDbContext(DbContextOptions<SymphonyDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options);
