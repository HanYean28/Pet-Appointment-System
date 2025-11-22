using Microsoft.AspNetCore.Mvc;
using PawfectGrooming.Models;
using System.Collections.Generic;
using System.Linq;

namespace PawfectGrooming;
public class FAQController : Controller
{
    private readonly UserContext _db;
    private readonly List<FAQ> _faqs;

    public FAQController(UserContext db)
    {
        _db = db;
    }

    public IActionResult Chatbox()
    {
        return View("~/Views/Home/FAQ.cshtml");
    }

    [HttpPost]
    public IActionResult GetAnswer([FromBody] string question)
    {
        if (string.IsNullOrEmpty(question))
        {
            return BadRequest(new { answer = "Please provide a question." });
        }

        string answer = FindAnswerByKeyword(question);
        return Ok(new { answer = answer });
    }

    private string FindAnswerByKeyword(string userQuestion)
    {
        string lowerQuestion = userQuestion.ToLower();

        // Loop through each FAQ entry to find a match.
        // ToList() materializes the query, which is fine for a small number of FAQs.
        var allFaqs = _db.FAQs.ToList();

        foreach (var faq in allFaqs)
        {
            // Split the comma-separated keywords from the database into a list of individual words.
            var keywords = faq.Keyword.ToLower().Split(',').Select(k => k.Trim());

            // Check if the user's question contains ANY of the individual keywords.
            if (keywords.Any(keyword => lowerQuestion.Contains(keyword)))
            {
                return faq.Answer;
            }
        }

        // If no match is found after checking all entries.
        return "Sorry I am not able to answer this, hope you have a Pawfect day!";
    }
}
