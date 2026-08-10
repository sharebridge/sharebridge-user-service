using Microsoft.Extensions.Configuration;
using SharingBridge.UserService;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/MintDevJwt -- <user_id> [initiator|coordinator]");
    return 1;
}

var userId = args[0].Trim();
var roleArg = args.Length > 1 ? args[1].Trim() : "initiator";
var role = roleArg == "donor" ? "initiator" : roleArg;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var tokens = new TokenService(config);
var roles = role == "coordinator"
    ? new[] { Roles.Donor, Roles.Coordinator }
    : new[] { Roles.Donor, Roles.Initiator };

Console.WriteLine(tokens.Mint(userId, role, roles));
return 0;
