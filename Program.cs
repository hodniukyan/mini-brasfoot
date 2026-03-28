using BrasfootAPI.Models;
using BrasfootAPI.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=brasfoot.db"));

var app = builder.Build();

app.UseCors(policy =>
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader());
// Swagger
app.UseSwagger();
app.UseSwaggerUI();
if (app.Environment.IsDevelopment())
{
    app.Urls.Add("http://localhost:5063");
}
else
{
    app.Urls.Add("http://0.0.0.0:8080");
}



// **-----------CRUD TIMES-----------**
// Lista todos os times
app.MapGet("/teams", async (AppDbContext db) =>
{
    var teams = await db.Teams.ToListAsync();
    return Results.Ok(teams);
});

// Pesquisa apenas um time pelo ID
app.MapGet("/teams/{id}", async (int id, AppDbContext db) =>
{
    var team = await db.Teams.FindAsync(id);

    if (team == null)
    {
        return Results.NotFound("Time não encontrado");
    }

    return Results.Ok(team);
});

app.MapPost("/teams", async (Team team, AppDbContext db) =>
{
    if (team.Strength <= 0)
        return Results.BadRequest("Força inválida");

    if (string.IsNullOrWhiteSpace(team.Name))
        return Results.BadRequest("Nome obrigatório");

    if (string.IsNullOrWhiteSpace(team.Country))
        return Results.BadRequest("País obrigatório");

    await db.Teams.AddAsync(team);
    await db.SaveChangesAsync();

    return Results.Created($"/teams/{team.Id}", team);
});

app.MapPut("/teams/{id}", async (int id, Team updatedTeam, AppDbContext db) =>
{
    var team = await db.Teams.FindAsync(id);

    if (team == null)
        return Results.NotFound("Time não encontrado");

    team.Name = updatedTeam.Name;
    team.Country = updatedTeam.Country;
    team.Strength = updatedTeam.Strength;

    await db.SaveChangesAsync();

    return Results.Ok(team);
});

app.MapDelete("/teams/{id}", async (int id, AppDbContext db) =>
{
    var team = await db.Teams.FindAsync(id);

    if (team == null)
        return Results.NotFound("Time não encontrado");

    db.Teams.Remove(team);
    await db.SaveChangesAsync();

    return Results.Ok("Time removido");
});

// **-----------JOGADORES-----------**
app.MapPost("/players", async (Player player, AppDbContext db) =>
{
    if (player.Skill <= 0)
        return Results.BadRequest("Skill inválida");

    if (string.IsNullOrWhiteSpace(player.Name))
        return Results.BadRequest("Nome obrigatório");

    if (string.IsNullOrWhiteSpace(player.Position))
        return Results.BadRequest("Posição é obrigatória");

    var teamExists = await db.Teams.AnyAsync(t => t.Id == player.TeamId);

    if (!teamExists)
        return Results.BadRequest("Time não existe");

    await db.Players.AddAsync(player);
    await db.SaveChangesAsync();

    return Results.Created($"/players/{player.Id}", player);
});

app.MapGet("/teams/{id}/players", async (int id, AppDbContext db) =>
{
    var teamExists = await db.Teams.AnyAsync(t => t.Id == id);

    if (!teamExists)
        return Results.NotFound("Time não encontrado");

    var players = await db.Players
        .Where(p => p.TeamId == id)
        .ToListAsync();

    return Results.Ok(players);
});

app.MapPut("/teams/{id}/players", async (int id, Player updatedPlayer, AppDbContext db) =>
{
    var player = await db.Players.FindAsync(id);

    if (player == null)
        return Results.NotFound("Time não encontrado");

    player.Name = updatedPlayer.Name;
    player.Age = updatedPlayer.Age;
    player.Skill = updatedPlayer.Skill;
    player.Position = updatedPlayer.Position;

    await db.SaveChangesAsync();

    return Results.Ok(player);
});

app.MapDelete("/teams/{id}/players", async (int id, AppDbContext db) =>
{
    var player = await db.Players.FindAsync(id);

    if (player == null)
        return Results.NotFound("Jogador não encontrado");

    db.Players.Remove(player);
    await db.SaveChangesAsync();

    return Results.Ok("Jogador removido");
});

// **-----------PARTIDAS-----------**
app.MapPost("/match", async (int team1Id, int team2Id, AppDbContext db) =>
{
    var team1 = await db.Teams.FindAsync(team1Id);
    var team2 = await db.Teams.FindAsync(team2Id);

    if (team1 == null || team2 == null)
        return Results.BadRequest("Times inválidos");

    var team1Players = await db.Players.Where(p => p.TeamId == team1Id).ToListAsync();
    var team2Players = await db.Players.Where(p => p.TeamId == team2Id).ToListAsync();

    // FILTRA QUEM PODE FAZER GOL (REMOVE GOLEIRO)
    var scorersTeam1 = team1Players.Where(p => p.Position != "GK").ToList();
    var scorersTeam2 = team2Players.Where(p => p.Position != "GK").ToList();

    // VALIDAÇÃO IMPORTANTE
    if (!scorersTeam1.Any() || !scorersTeam2.Any())
        return Results.BadRequest("Times precisam ter jogadores de linha");

    var team1Strength = team1.Strength + team1Players.Sum(p => p.Skill);
    var team2Strength = team2.Strength + team2Players.Sum(p => p.Skill);

    var random = new Random();

    int team1Goals = 0;
    int team2Goals = 0;

    int chances = 5;

    // SIMULAÇÃO DAS JOGADAS
    for (int i = 0; i < chances; i++)
    {
        var chance1 = random.Next(0, team1Strength);
        var chance2 = random.Next(0, team2Strength);

        if (chance1 > chance2)
            team1Goals++;
        else if (chance2 > chance1)
            team2Goals++;
    }

    // ALEATORIEDADE EXTRA
    team1Goals += random.Next(0, 2);
    team2Goals += random.Next(0, 2);

    // LIMITE DE GOLS
    team1Goals = Math.Min(team1Goals, 6);
    team2Goals = Math.Min(team2Goals, 6);

    var goals = new List<object>();

    // GOLS TIME 1
    for (int i = 0; i < team1Goals; i++)
    {
        var player = scorersTeam1[random.Next(scorersTeam1.Count)];

        goals.Add(new
        {
            team = team1.Name,
            player = player.Name
        });
    }

    // GOLS TIME 2
    for (int i = 0; i < team2Goals; i++)
    {
        var player = scorersTeam2[random.Next(scorersTeam2.Count)];

        goals.Add(new
        {
            team = team2.Name,
            player = player.Name
        });
    }

    return Results.Ok(new
    {
        Team1 = team1.Name,
        Team2 = team2.Name,
        Score = $"{team1Goals} x {team2Goals}",
        Goals = goals
    });
});

app.Run();