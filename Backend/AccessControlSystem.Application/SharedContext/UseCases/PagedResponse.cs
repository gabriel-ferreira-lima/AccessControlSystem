using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Application.SharedContext.UseCases {
    public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int Size, int Total) {
        public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)Size);
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;
    }
}