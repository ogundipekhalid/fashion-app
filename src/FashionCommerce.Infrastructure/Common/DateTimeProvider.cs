using FashionCommerce.Application.Common.Interfaces;

namespace FashionCommerce.Infrastructure.Common;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
