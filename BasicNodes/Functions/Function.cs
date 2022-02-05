namespace FileFlows.BasicNodes.Functions
{
    using System.ComponentModel;
    using FileFlows.Plugin;
    using FileFlows.Plugin.Attributes;
    using Jint.Runtime;
    using Jint.Native.Object;
    using Jint;
    using System.Text;
    using System.ComponentModel.DataAnnotations;
    using System.Text.RegularExpressions;
    using System.Text.Json;

    public class Function : Node
    {
        public override int Inputs => 1;
        public override FlowElementType Type => FlowElementType.Logic;
        public override string Icon => "fas fa-code";

        public override string HelpUrl => "https://github.com/revenz/FileFlows/wiki/Function-Node"; 

        [DefaultValue(1)]
        [NumberInt(1)]
        public new int Outputs { get; set; }

        [Required]
        [DefaultValue("// Custom javascript code that you can run against the flow file.\n// Flow contains helper functions for the Flow.\n// Variables contain variables available to this node from previous nodes.\n// Logger lets you log messages to the flow output.\n\n// return 0 to complete the flow.\n// return -1 to signal an error in the flow\n// return 1+ to select which output node will be processed next\n\nif(Variables.file.Size === 0)\n\treturn -1;\n\nreturn 0;")]
        [Code(2)]
        public string Code { get; set; }

        delegate void LogDelegate(params object[] values);
        public override int Execute(NodeParameters args)
        {
            if (string.IsNullOrEmpty(Code))
                return -1; // no code, flow cannot continue doesnt know what to do

            try
            {
                long fileSize = 0;
                var fileInfo = new FileInfo(args.WorkingFile);
                if (fileInfo.Exists)
                    fileSize = fileInfo.Length;

                // replace Variables. with dictionary notation
                string tcode = Code;
                foreach (string k in args.Variables.Keys.OrderByDescending(x => x.Length))
                {
                    // replace Variables.Key or Variables?.Key?.Subkey etc to just the variable
                    // so Variables.file?.Orig.Name, will be replaced to Variables["file.Orig.Name"] 
                    // since its just a dictionary key value 
                    string keyRegex = @"Variables(\?)?\." + k.Replace(".", @"(\?)?\.");


                    object? value = args.Variables[k];
                    if (value is JsonElement jElement)
                    {
                        if (jElement.ValueKind == JsonValueKind.String)
                            value = jElement.GetString();
                        if (jElement.ValueKind == JsonValueKind.Number)
                            value = jElement.GetInt64();
                    }

                    tcode = Regex.Replace(tcode, keyRegex, "Variables['" + k + "']");
                }

                var sb = new StringBuilder();
                var log = new
                {
                    ILog = new LogDelegate(args.Logger.ILog),
                    DLog = new LogDelegate(args.Logger.DLog),
                    WLog = new LogDelegate(args.Logger.WLog),
                    ELog = new LogDelegate(args.Logger.ELog),
                };
                var engine = new Engine(options =>
                {
                    options.LimitMemory(4_000_000);
                    options.MaxStatements(500);
                })
                .SetValue("Logger", args.Logger)
                .SetValue("Variables", args.Variables)
                .SetValue("Flow", args);
                var result = int.Parse(engine.Evaluate(tcode).ToObject().ToString());
                return result;
            }
            catch (Exception ex)
            {
                args.Logger?.ELog("Failed executing function: " + ex.Message + Environment.NewLine + ex.StackTrace);
                return -1;
            }
        }
    }
}