using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence
{
    public sealed class UtcDateTimeConverter () : ValueConverter<DateTime, DateTime>
        (
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
}
