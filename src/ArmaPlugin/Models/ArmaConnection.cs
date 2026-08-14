namespace EdenOnline;

public class ArmaSyncConnection {
    public string FromID { get; set; } = string.Empty;
    public string ToID { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public ArmaSyncConnection() {}

    public ArmaSyncConnection(string fromID, string toID, string type) {
        FromID = fromID;
        ToID = toID;
        Type = type;
    }
}