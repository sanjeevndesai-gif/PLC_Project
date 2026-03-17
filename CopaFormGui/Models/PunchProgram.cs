namespace CopaFormGui.Models;

public class PunchStep
{
    public int StepNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public int ToolId { get; set; }
    public string Operation { get; set; } = "Punch";
    public bool IsCompleted { get; set; }
}

public class PunchProgram
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = string.Empty;
    public List<PunchStep> Steps { get; set; } = new();
    public int TotalStrokes => Steps.Count;
    public int CompletedStrokes => Steps.Count(s => s.IsCompleted);
}
