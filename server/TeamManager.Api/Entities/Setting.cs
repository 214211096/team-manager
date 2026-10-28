namespace TeamManager.Api.Entities;

/// <summary>הגדרת מערכת כמפתח-ערך, נערכת מהמסך בלי שינוי קוד.</summary>
public class Setting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
