using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Internet
{
    public class InternetHelper
    {
        public static string TestUrl { get; set; } = "http://www.google.com"; // Replace with the URL you want to check

        public static async Task<bool> AvailableAsync(int timeout = 1000)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromMilliseconds(timeout);
                    HttpResponseMessage response = await client.GetAsync(TestUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (HttpRequestException ex)
                {
                    return false;
                }
            }
        }

        public static void OpenUrl(string url)
        {
            // https://stackoverflow.com/questions/502199/how-to-open-a-web-page-from-my-application
            // For .NET Core, the default for ProcessStartInfo.UseShellExecute has changed from true to false, and so you have to explicitly set it to true for this to work;
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}
