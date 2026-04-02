using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace lab2.Tests
{
    public class HealthTests
    {
        [Fact]
        public async Task Health_Endpoint_Returns_OK()
        {
            var baseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL")
                          ?? "http://localhost:8080";

            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

