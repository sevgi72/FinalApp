using BankApiApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BankAppTest.TestHelpers;

/// <summary>
/// Testler ucun yaddasda islenen (InMemory) baza yaradir.
/// Her test OZ bazasini alir — testler bir-birine tesir etmir.
/// </summary>
public static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            // her cagirisda unikal ad — testler izole olur
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // InMemory provayderi tranzaksiyani desteklemir ve default olaraq
            // exception atir. Servislerimiz BeginTransactionAsync isledir,
            // ona gore bu xeberdarligi susduruq (tranzaksiya no-op olur).
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}