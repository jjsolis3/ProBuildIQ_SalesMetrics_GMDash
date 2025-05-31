using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;
//using OpenAI_API;
//using OpenAI_API.Completions;

namespace SalesMetrics.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IConfiguration _configuration;

        public ReportsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Ask()
        {
            return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> Ask(string query)
        //{
        //    string connectionString = _configuration.GetConnectionString("SalesMetrics");

        //    // 🔸 Ask OpenAI to convert the query to SQL
        //    string prompt = $@"
        //        You are a SQL assistant. Convert the following question into a SQL query:
        //        Database: SalesMetrics. Tables include: Tasks, Users, INVOICE_HEADER, AR_OPEN_ITEM, CUSTOMER_MASTER, SALESMAN_MASTER.
        //        Only return SQL. No explanation.

        //        Question: {query}
        //        SQL:";

        //    var api = new OpenAIAPI("YOUR_OPENAI_API_KEY");  // Replace with real key or use Secret Manager
        //    var completion = await api.Completions.CreateCompletionAsync(new CompletionRequest
        //    {
        //        Prompt = prompt,
        //        MaxTokens = 200,
        //        Temperature = 0,
        //        Model = "text-davinci-003"
        //    });

        //    string sql = completion.Completions.FirstOrDefault()?.Text?.Trim() ?? "";

        //    // Execute the query
        //    var resultTable = new DataTable();
        //    try
        //    {
        //        using var conn = new SqlConnection(connectionString);
        //        using var cmd = new SqlCommand(sql, conn);
        //        using var adapter = new SqlDataAdapter(cmd);
        //        conn.Open();
        //        adapter.Fill(resultTable);
        //    }
        //    catch (Exception ex)
        //    {
        //        ViewBag.Error = $"Query failed: {ex.Message}";
        //        return View();
        //    }

        //    ViewBag.Query = query;
        //    ViewBag.Sql = sql;
        //    return View("Results", resultTable);
        //}
    }
}
