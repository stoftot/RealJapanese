namespace Repositories.DTOs;

public class FillInBlanksDto
{
    public string Header { get; set; }
    public string Template { get; set; }
    public List<string> Answers { get; set; }
}