using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using OnlineBankingAPI.Assistant;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Repositories;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Tests;

/// <summary>Keeps the layers from Layered architecture in the design: controllers → services → repositories.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Api = typeof(AccountService).Assembly;

    private static IEnumerable<Type> InNamespace(string ns) =>
        Api.GetTypes().Where(t => t.Namespace == ns && t is { IsClass: true } && !t.IsNested
                                  && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));

    private static IEnumerable<Type> Dependencies(Type t) =>
        t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

    private static bool IsRepository(Type t) => t.Namespace == typeof(IAccountRepository).Namespace;

    [Fact]
    public void Controllers_depend_only_on_services()
    {
        var offenders = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(c => Dependencies(c).Where(d => d == typeof(BankDbContext) || IsRepository(d))
                .Select(d => $"{c.Name} -> {d.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Services_do_not_expose_IQueryable_and_repositories_do_not_return_it()
    {
        var types = InNamespace(typeof(AccountService).Namespace!)
            .Concat(Api.GetTypes().Where(IsRepository));
        var offenders = types
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => ReturnsQueryable(m.ReturnType))
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_repository_interface_has_one_implementation()
    {
        var interfaces = Api.GetTypes().Where(t => IsRepository(t) && t.IsInterface).ToList();
        Assert.Equal(6, interfaces.Count);
        foreach (var i in interfaces)
            Assert.Single(Api.GetTypes(), t => t is { IsClass: true, IsAbstract: false } && i.IsAssignableFrom(t));
    }

    [Fact]
    public void The_assistant_can_only_reach_read_only_account_services()
    {
        var assistant = Api.GetTypes()
            .Where(t => t.Namespace?.StartsWith(typeof(AssistantService).Namespace!) == true)
            .ToList();
        Assert.NotEmpty(assistant);

        // Nothing that can write: no transfers, sessions or auth, no repositories, no DbContext.
        Type[] forbidden = [typeof(TransferService), typeof(TransferValidator), typeof(PayeeService),
            typeof(AuthService), typeof(SessionService), typeof(BankDbContext), typeof(DbSeeder)];
        var offenders = assistant
            .SelectMany(t => Dependencies(t).Where(d => forbidden.Contains(d) || IsRepository(d))
                .Select(d => $"{t.Name} -> {d.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool ReturnsQueryable(Type t)
    {
        if (typeof(IQueryable).IsAssignableFrom(t)) return true;
        return t.IsGenericType && t.GetGenericArguments().Any(ReturnsQueryable);
    }
}
