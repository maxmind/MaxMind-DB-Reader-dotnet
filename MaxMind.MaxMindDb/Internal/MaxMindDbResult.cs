using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MaxMind.GeoIP2
{
    public class MaxMindDbResult
    {
        public MaxMindDbResultNode Node { get; set; }

        public int Offset { get; set; }

        public MaxMindDbResult(MaxMindDbResultNode node, int offset)
        {
            this.Node = node;
            this.Offset = offset;
        }

        public bool TryGetStringDictionary(out Dictionary<string, MaxMindDbResultNode> dict)
        {
            dict = new Dictionary<string, MaxMindDbResultNode>();

            try
            {
                var local = (Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>)this.Node.Value;

                foreach (MaxMindDbResultNode node in local.Keys)
                {
                    string key = ((MaxMindDbResultNodeGeneric<string>)node).GetValue();
                    MaxMindDbResultNode value = local[node];
                    dict.Add(key, value);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string ToJson()
        {
            return "{ " + this.Node.ToJson() + " }";
        }

        public static MaxMindDbResult Create(MaxMindDbResultNode node, int offset)
        {
            return new MaxMindDbResult(node, offset);
        }

        public static MaxMindDbResult Create<T>(T value, int offset)
        {
            return new MaxMindDbResult(new MaxMindDbResultNodeGeneric<T>(value), offset);
        }
    }

    public class MaxMindDbResultNode
    {
        public object Value { get; set; }

        public MaxMindDbResultNode(object value) {
            this.Value = value;
        }

        public Type GetValueType()
        {
            return this.Value.GetType();
        }

        public string ToJson()
        {
            if (this.Value is List<MaxMindDbResultNode>)
            {
                List<string> list = new List<string>();

                foreach (var node in ((List<MaxMindDbResultNode>)this.Value)) 
                {
                    if (node.Value is Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>)
                        list.Add(String.Format("{{ {0} }}", node.ToJson()));
                    else
                        list.Add(node.ToJson());

                }

                return String.Format("[ {0} ]", String.Join(", ", list.ToArray()));

            }
            else if (this.Value is Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>)
            {
                var dict = new Dictionary<string, MaxMindDbResultNode>();

                List<string> list = new List<string>();

                if (TryGetStringDictionary(out dict))
                {
                    foreach (string key in dict.Keys)
                    {
                        var node = dict[key].Value;

                        if(node is Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>)
                            list.Add(String.Format(" \"{0}\": {{ {1} }}", key, dict[key].ToJson()));
                        else
                            list.Add(String.Format("\"{0}\": {1}", key, dict[key].ToJson()));
                    }
                }

                //if (list.Count == 1)
                //    return String.Format("{{ {0} }}", list[0]);

                return String.Join(", ", list.ToArray());
            }
            else
            {
                return String.Format("\"{0}\"", this.Value);
            }
        }

        public bool TryGetStringDictionary(out Dictionary<string, MaxMindDbResultNode> dict)
        {
            dict = new Dictionary<string, MaxMindDbResultNode>();

            try
            {
                var local = (Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>)this.Value;

                foreach (MaxMindDbResultNode node in local.Keys)
                {
                    string key = ((MaxMindDbResultNodeGeneric<string>)node).GetValue();
                    MaxMindDbResultNode value = local[node];
                    dict.Add(key, value);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class MaxMindDbResultNodeGeneric<T> : MaxMindDbResultNode
    {
        public MaxMindDbResultNodeGeneric(T value) : base(value) { }

        public T GetValue()
        {
            return (T)this.Value;
        }
    }
}
