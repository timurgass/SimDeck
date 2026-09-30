using System.IO;
using System.Text;
using SimDeck.Core;

static class Fs25PlanTests
{
    static Stream Utf8(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    const string MinimalPlan = """
        {
          "planVersion": 1,
          "name": "Осень первого года",
          "forPeriods": [7, 8, 9],
          "tasks": [
            {
              "id": "harvest-46-autumn1",
              "title": "Убрать пшеницу с поля 46",
              "why": "Поспела в август — тянуть нельзя.",
              "kit": "Комбайн есть.",
              "priority": 1,
              "field": 46,
              "deckPage": "Посев"
            },
            {
              "id": "buy-lime-autumn1",
              "title": "Купить разбрасыватель извести",
              "why": "Без него поле 45 не подготовить.",
              "priority": 2
            }
          ],
          "dont": [
            { "title": "Не продавать пшеницу сразу", "why": "273 сейчас, 408 к январю." }
          ],
          "letters": [
            {
              "id": "l1",
              "date": "6-й день · август",
              "subject": "О вашей пшенице",
              "body": ["Уважаемый клиент,", "К сути."],
              "sign": "Ваш Весельчак"
            }
          ]
        }
        """;

    public static void Run(Action<bool, string> check)
    {
        // ---- Happy path ----

        Fs25Plan plan;
        using (var s = Utf8(MinimalPlan)) plan = Fs25PlanLoader.Load(s);
        check(plan.PlanVersion == 1 && plan.Name == "Осень первого года",
            "Loader reads plan version and name");
        check(plan.ForPeriods.SequenceEqual(new[] { 7, 8, 9 }),
            "Loader preserves forPeriods and sorts them");
        check(plan.Tasks.Count == 2 && plan.Tasks[0].Field == 46 && plan.Tasks[0].DeckPage == "Посев",
            "Task ids, fields, deckPage are parsed");
        check(plan.Dont.Count == 1 && plan.Letters.Count == 1 && plan.Letters[0].Body.Count == 2,
            "Dont list and letters (with body paragraphs) are preserved");
        check(plan.AppliesTo(new Period(7)) && !plan.AppliesTo(new Period(3)),
            "AppliesTo returns true only for periods in forPeriods");

        // ---- Version mismatch ----

        var v2Bad = MinimalPlan.Replace("\"planVersion\": 1", "\"planVersion\": 2");
        try { using var s = Utf8(v2Bad); Fs25PlanLoader.Load(s); check(false, "Version 2 rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("не поддерживается"), $"Version mismatch has friendly message: {ex.Message}"); }

        // ---- Priority out of range ----

        var badPri = MinimalPlan.Replace("\"priority\": 1", "\"priority\": 0");
        try { using var s = Utf8(badPri); Fs25PlanLoader.Load(s); check(false, "priority 0 rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("Разрешено 1, 2 или 3"), $"priority 0 error: {ex.Message}"); }

        // ---- deckPage from wrong list ----

        var badPage = MinimalPlan.Replace("\"deckPage\": \"Посев\"", "\"deckPage\": \"Клавиатура\"");
        try { using var s = Utf8(badPage); Fs25PlanLoader.Load(s); check(false, "Bad deckPage rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("не из списка"), $"Bad deckPage error: {ex.Message}"); }

        // ---- Duplicate task id ----

        var dupId = MinimalPlan.Replace("buy-lime-autumn1", "harvest-46-autumn1");
        try { using var s = Utf8(dupId); Fs25PlanLoader.Load(s); check(false, "Duplicate id rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("Повторяющийся id"), $"Dup id error: {ex.Message}"); }

        // ---- Empty tasks ----

        var noTasks = "{ \"planVersion\": 1, \"name\": \"x\", \"forPeriods\": [7], \"tasks\": [] }";
        try { using var s = Utf8(noTasks); Fs25PlanLoader.Load(s); check(false, "Empty tasks rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("tasks"), $"Empty tasks error: {ex.Message}"); }

        // ---- forPeriods out of range ----

        var badPeriod = MinimalPlan.Replace("[7, 8, 9]", "[7, 13]");
        try { using var s = Utf8(badPeriod); Fs25PlanLoader.Load(s); check(false, "Period 13 rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("вне диапазона"), $"Period out of range: {ex.Message}"); }

        // ---- Trailing comma → JsonException wrapped ----

        var trailing = MinimalPlan.Replace("\"sign\": \"Ваш Весельчак\"", "\"sign\": \"Ваш Весельчак\",");
        try { using var s = Utf8(trailing); Fs25PlanLoader.Load(s); check(false, "Trailing comma rejected"); }
        catch (Fs25PlanException ex) { check(ex.Message.Contains("JSON не разобрать"), $"Trailing comma: {ex.Message}"); }

        // ---- Markdown fence wrapper ----

        var fenced = "```json\n" + MinimalPlan + "\n```";
        try { using var s = Utf8(fenced); Fs25PlanLoader.Load(s); check(false, "```json``` fence rejected"); }
        catch (Fs25PlanException) { check(true, "Common ```json``` fence gives a friendly error, not a stack trace"); }
    }
}
