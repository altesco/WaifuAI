using System;
using System.Net.Http;

namespace WaifuAI.Services;

public static class ApiService
{
    public static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
}
