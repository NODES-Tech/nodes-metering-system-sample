using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;
using static System.String;

namespace Nodes.MeteringSystem.Sample
{
    public class TokenProvider : ITokenProvider
    {
        private readonly IConfiguration _configRoot;
        private readonly HttpClient _httpClient;

        public TokenProvider(IConfiguration configRoot, HttpClient httpClient)
        {
            _configRoot = configRoot;
            _httpClient = httpClient;
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var section = _configRoot.GetSection("Authentication");
            var req = new ClientCredentialsTokenRequest
            {
                Address = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.Address)),
                Scope = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.Scope)),
                ClientId = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.ClientId)),
                ClientSecret = section.GetValue<string>(nameof(ClientCredentialsTokenRequest.ClientSecret)),
            };
            TokenResponse response = await _httpClient.RequestClientCredentialsTokenAsync(req, cancellationToken);
            if (IsNullOrEmpty(response.AccessToken) || response.Error != null)
                throw new Exception(response.Error + "; " + response.ErrorDescription);
            Console.WriteLine("Acquired new token: " + response.AccessToken);
            return new AuthenticationHeaderValue("Bearer", response.AccessToken);
        }
    }
}