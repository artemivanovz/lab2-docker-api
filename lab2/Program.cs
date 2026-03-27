using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "studentsdb";
var username = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";

var connectionString =
    $"Host={host};Port={port};Database={database};Username={username};Password={password}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/groups", async (AppDbContext db) =>
    await db.Groups
        .Select(g => new
        {
            g.Id,
            g.Name
        })
        .ToListAsync());

app.MapGet("/students", async (AppDbContext db) =>
    await db.Students
        .Include(s => s.Group)
        .Select(s => new
        {
            s.Id,
            s.FullName,
            s.RecordBookNumber,
            s.Score,
            s.GroupId,
            GroupName = s.Group != null ? s.Group.Name : null
        })
        .ToListAsync());
app.MapPost("/groups", async (AppDbContext db, Group group) =>
{
    var newGroup = new Group
    {
        Name = group.Name
    };

    db.Groups.Add(newGroup);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        newGroup.Id,
        newGroup.Name
    });
});

app.MapPost("/students", async (AppDbContext db, Student student) =>
{
    db.Students.Add(student);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        student.Id,
        student.FullName,
        student.RecordBookNumber,
        student.Score,
        student.GroupId
    });
});

app.MapDelete("/students/{id}", async (AppDbContext db, int id) =>
{
    var student = await db.Students.FindAsync(id);

    if (student == null)
        return Results.NotFound();

    db.Students.Remove(student);
    await db.SaveChangesAsync();

    return Results.Ok();
});

app.MapGet("/students/sort", async (AppDbContext db, string field = "Id", string dir = "asc") =>
{
    var q = db.Students.Include(s => s.Group).AsQueryable();

    bool asc = dir.ToLower() == "asc";

    q = (field, asc) switch
    {
        ("FullName", true) => q.OrderBy(s => s.FullName),
        ("FullName", false) => q.OrderByDescending(s => s.FullName),

        ("Score", true) => q.OrderBy(s => s.Score),
        ("Score", false) => q.OrderByDescending(s => s.Score),

        _ when asc => q.OrderBy(s => s.Id),
        _ => q.OrderByDescending(s => s.Id)
    };

    return Results.Ok(await q.ToListAsync());
});

app.MapGet("/students/filter", async (AppDbContext db, string field, string op, string value) =>
{
    var q = db.Students.Include(s => s.Group).AsQueryable();

    if (field == "FullName")
    {
        if (op == "contains")
            q = q.Where(s => s.FullName.Contains(value));

        if (op == "=")
            q = q.Where(s => s.FullName == value);
    }

    if (field == "Score")
    {
        int x = int.Parse(value);

        if (op == ">")
            q = q.Where(s => s.Score > x);

        if (op == "<")
            q = q.Where(s => s.Score < x);

        if (op == "=")
            q = q.Where(s => s.Score == x);
    }

    return Results.Ok(await q.ToListAsync());
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.Migrate();

    if (!db.Groups.Any())
    {
        var g1 = new Group { Name = "IVT21" };
        var g2 = new Group { Name = "IVT22" };

        db.Groups.AddRange(g1, g2);
        db.SaveChanges();

        db.Students.AddRange(

            new Student
            {
                FullName = "Ivan Ivanov",
                RecordBookNumber = "A101",
                Score = 70,
                GroupId = g1.Id
            },

            new Student
            {
                FullName = "Petr Petrov",
                RecordBookNumber = "A102",
                Score = 85,
                GroupId = g2.Id
            }
        );

        db.SaveChanges();
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.Run();

public class Group
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public List<Student> Students { get; set; } = new();
}

public class Student
{
    public int Id { get; set; }

    public string FullName { get; set; } = "";

    public string RecordBookNumber { get; set; } = "";

    public int Score { get; set; }

    public int GroupId { get; set; }

    public Group? Group { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<Student> Students => Set<Student>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Group>()
            .HasMany(g => g.Students)
            .WithOne(s => s.Group)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Student>()
            .HasIndex(s => s.RecordBookNumber)
            .IsUnique();
    }
}