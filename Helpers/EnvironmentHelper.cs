namespace LyuSyncConfiguration.Helpers;

/// <summary>
/// 环境检测辅助类
/// </summary>
public static class EnvironmentHelper
{
    /// <summary>
    /// 自动检测当前环境名称
    /// 优先级: DOTNET_ENVIRONMENT > ASPNETCORE_ENVIRONMENT > 默认值
    /// </summary>
    /// <param name="defaultEnvironment">默认环境名称（如果未检测到）</param>
    /// <returns>环境名称</returns>
    public static string? GetEnvironment(string? defaultEnvironment = null)
    {
        // 检查 DOTNET_ENVIRONMENT（通用 .NET 应用）
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        
        if (string.IsNullOrEmpty(env))
        {
            // 检查 ASPNETCORE_ENVIRONMENT（ASP.NET Core 应用）
            env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }

        return string.IsNullOrEmpty(env) ? defaultEnvironment : env;
    }

    /// <summary>
    /// 判断当前是否为开发环境
    /// </summary>
    public static bool IsDevelopment()
    {
        var env = GetEnvironment();
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断当前是否为生产环境
    /// </summary>
    public static bool IsProduction()
    {
        var env = GetEnvironment();
        return string.IsNullOrEmpty(env) || 
               string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);
    }
}
