namespace ChecksumNodes;

public class Plugin : IPlugin
{
    public Guid Uid => new Guid("5ce1524c-5e7b-40ee-9fc1-2152181490f1");
    public string Name => "Checksum Nodes";
    public string MinimumVersion => "0.7.0.0";

    public void Init()
    {
    }
}
