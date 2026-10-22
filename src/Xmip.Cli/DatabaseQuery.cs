namespace Xmip.Cli;

/// <summary>What <c>xmip database</c> was asked
/// (<see cref="DatabaseArguments"/>).</summary>
/// <param name="Server">The database server: <c>postgresql</c>, <c>mssql</c>
/// or <c>mariadb</c>, as the line named it.</param>
/// <param name="From">The schema version the databases hold.</param>
/// <param name="To">The version to bring them to; zero for this build's.</param>
/// <param name="Out">The folder the scripts are written to; null to print
/// them.</param>
public sealed record DatabaseQuery(string Server, uint From, uint To, string? Out);
