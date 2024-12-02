// Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Serilog.Events;

namespace Serilog.Sinks.Seq;

/// <summary>
/// Enables switching between the experimental "unflattening" behavior applied to dotted property names, and the
/// regular verbatim property name handling.
/// </summary>
interface IDottedPropertyNameConvention
{
    /// <summary>
    /// Convert the properties in <paramref name="maybeDotted"/> into the form specified by the current property
    /// name processing convention.
    /// </summary>
    /// <param name="maybeDotted">The properties associated with a log event.</param>
    /// <returns>The processed properties.</returns>
    IReadOnlyDictionary<string, LogEventPropertyValue> ProcessDottedPropertyNames(IReadOnlyDictionary<string, LogEventPropertyValue> maybeDotted);
}
