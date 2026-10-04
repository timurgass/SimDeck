using System.Collections.Generic;
namespace TsMap
{
    public class TsNavCurve
    {
        public float X1, Z1, X2, Z2, Length;
        public float[] StartTangent, EndTangent;
        public List<int> Next;
    }
}
