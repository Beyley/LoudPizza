
namespace LoudPizza.Core
{
    public abstract class Filter
    {
        public enum ParamType
        {
            Float = 0,
            Int,
            Bool,
        }

        public virtual int GetParamCount()
        {
            return 1; // there's always WET
        }

        public virtual string GetParamName(uint paramIndex)
        {
            return "Wet";
        }

        public virtual ParamType GetParamType(uint paramIndex)
        {
            return ParamType.Float;
        }

        public virtual float GetParamMax(uint paramIndex)
        {
            return 1;
        }

        public virtual float GetParamMin(uint paramIndex)
        {
            return 0;
        }

        public abstract FilterInstance CreateInstance();
    }
}
