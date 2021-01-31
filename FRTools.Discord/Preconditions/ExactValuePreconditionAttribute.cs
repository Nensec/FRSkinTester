using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FRTools.Discord.Preconditions
{
    public class ExactValuePreconditionAttribute : ParameterPreconditionAttribute
    {
        public ExactValuePreconditionAttribute(object exactValue)
        {
            ExactValue = exactValue;
        }
        public object ExactValue { get; }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
        {
            if (parameter.IsOptional && value == null)
                return Task.FromResult(PreconditionResult.FromSuccess());

            if (ExactValue is IEnumerable<string> ExactValueArray)
            {
                foreach (var exactValue in ExactValueArray)
                {
                    if (value.Equals(exactValue))
                        return Task.FromResult(PreconditionResult.FromSuccess());
                }
                return Task.FromResult(PreconditionResult.FromError($"**{value}** must equal any of the following: **{string.Join("**, **", ExactValueArray)}**"));
            }
            else
                return Task.FromResult(value.Equals(ExactValue) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"**{value}** must equal **{ExactValue}**"));
        }
    }
}
