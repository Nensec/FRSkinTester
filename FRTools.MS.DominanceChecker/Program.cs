using FRTools.Common;
using FRTools.Common.Jobs;
using FRTools.Data;
using FRTools.Data.DataModels.FlightRisingModels;
using FRTools.Data.Messages;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using NLog;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FRTools.MS.DominanceChecker
{
    class Program
    {
        private static IQueueClient _serviceBus;
        private static FRToolsLogger _logger;

        static async Task Main()
        {
            FRToolsLogger.Setup(Data.DataModels.LogItemOrigin.DominanceChecker);
            _logger = LogManager.LogFactory.GetLogger<FRToolsLogger>("DominanceChecker");

            _serviceBus = new QueueClient(ConfigurationManager.AppSettings["AzureSBConnString"], ConfigurationManager.AppSettings["AzureSBQueueName"]);
            await _serviceBus.SendAsync(new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new GenericMessage(MessageCategory.DominanceTracker, "Started")))));
            while (true)
            {
                try
                {
                    Flight[] positions = new Flight[3];
                    for (var i = 0; i <= 2; i++)
                    {
                        var request = (HttpWebRequest)WebRequest.Create("https://flightrising.com/includes/ol/dom_text.php");
                        var data = Encoding.ASCII.GetBytes($"position={i + 1}");
                        request.Method = "POST";
                        request.ContentType = "application/x-www-form-urlencoded";
                        request.ContentLength = data.Length;

                        using (var stream = request.GetRequestStream())
                            stream.Write(data, 0, data.Length);

                        var response = (HttpWebResponse)request.GetResponse();

                        using (var streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            var responseString = streamReader.ReadToEnd();

                            var parse = Regex.Match(responseString, @"<span[\s\S]*>([a-zA-Z]+)</span>");
                            positions[i] = (Flight)Enum.Parse(typeof(Flight), parse.Groups[1].Value);
                            Console.WriteLine($"Position: {i + 1} = {positions[i]}");
                        }
                    }
                    using (var ctx = new DataContext())
                    {
                        var previousDom = ctx.FRDominances.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                        if (previousDom == null || previousDom.First != (int)positions[0] || previousDom.Second != (int)positions[1] || previousDom.Third != (int)positions[2])
                        {
                            ctx.FRDominances.Add(new FRDominance { Timestamp = DateTime.UtcNow, First = (int)positions[0], Second = (int)positions[1], Third = (int)positions[2] });
                            await ctx.SaveChangesAsync();

                            var (JobId, StartTime, Task) = JobManager.StartNewJob(new SendUpdateDominanceMessage());
                            while (!Task.IsCompleted)
                                await Task.Delay(100);
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Results were the same, not updated yet?");
                            await _serviceBus.SendAsync(new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new GenericMessage(MessageCategory.DominanceTracker, "Same result")))));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                Console.WriteLine("Waiting 30 seconds to try again..");
                await Task.Delay(30000);
            }
        }
    }
}
