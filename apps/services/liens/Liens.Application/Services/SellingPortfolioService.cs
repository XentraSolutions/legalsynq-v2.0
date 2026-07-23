using System.Globalization;
using System.Net;
using System.Text;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Notifications;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class SellingPortfolioService : ISellingPortfolioService
{
    private const string LegalSynqBrandIconContentId = "legalsynq-brand-icon";
    private const string SellerInformationIconContentId = "seller-information-icon";
    private const string AssetOverviewIconContentId = "asset-overview-icon";
    private const string SupportingDocumentsIconContentId = "supporting-documents-icon";
    private const string LegalSynqLogoWhitePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAZcAAAB0CAYAAABJwX+VAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAOdEVYdFNvZnR3YXJlAEZpZ21hnrGWYwAAFV9JREFUeAHtne1x3DjSx3u27vvqInjgCNaOYKkIThuB6QgsR+BxBNZGMOMIbEcw3AikjWD4RGBdBH1sAVzRFEAAJPg6/18VSyoSBDEgiAbQL9jRBmDmq+rPTXX8Xh1ZdaiA2x6r46E6vlfHt91uVxIAAAAgVILlY3X84OEcqkMRAACAy0UEQXXcc1pESN0SAACAy8MIljOPx0cCAABwOUwgWGowgwEAgEuh6vRPPA2yRKYIAADAtqk6+5yn5UQAAACi2dGKqDr7++rPa5qWN7vd7oEAAAAE8wutBLNEFSJYPlXHq10H1fV/V8e76igD8rshAAAA26QSLrcBy1h5ZJ4hxgFYGgMAgK1SdfJ3HiFwoB5U92WefH8QAACAKFazLFbxm+f639QPnz7linV4GQAAAIGsSbj4eKQeVCqYkPsgXAAAIIItCRcAAAALAcIFAABAciBcAAAAJOdftB6+VMdfHdfh6AgAAGA5sB9FAAAAgsGyGAAAgORAuAAAAEgOhAsAAIDkzBoV2Xi+Z9WhaBpHxYfdbveNXpaDPff9Sd1OmnLtweTfy5kTAADAQFjvy3Li6Tk4ypOSE0cG0AQAADAA1kEizzwfUwiXmjPDygwAcKFMpnOpOtqP1R8JX6/oMlDVcTa/GwAALopJnCjNjCGny2Rf/X4JkPmJAADgQhh95mJG7jldNiJgbgkAAC6EUYVL1aFm1Z89AeEjdDAAgEth7JlLr90hN4qYWqM+AAAXwWg6F9bmuCoweUF6J8mxfUT6Brfs8nMRoSG7ZGbkR6zlskr/UhAAAIB4OMyP5bSEpaKAcqqAPBSHmVnfEQAAgHiqDvQqoJO9p4UQUFYVmE+IgDkTAABsnLF0Lq8D0vxBG6Na7iqrP+88yUQATRHqBgAAZmMs4aI81wvTEW8Oo0/x6Y4UAQDAhhlLuPhG5iVtm9JzHTMXAMCmGcta7NI7T0RGBpuC/cFYvyEiOGgySfiXC+QDdQvYvibRAMyFz0erIAyqQAMIlxGoRnAQHgAEYIxbFL00AhJBVeJbWi8QLsCJ2E17klzDIRTEYgTKe9KOx5knrfwpquNL1daOBFYDtjkGAEyC8X/7XP0rvl57CotqQSbdwfiQ5QRWAYQLAGB0KqEgy17iOC3Rwfsa/CjSQuaeEQR28axmWcxMpeeyQlP+FaJx2Ko/ELgcqm/npvrzldIhgupU5XuN72O5BAkXM+qQw9bBP1Qv+BuNjzTQuaIKn2gmjFAT5aYoNr/Qhh1QwfYwM4yu77aoju+k23dJz32M9Df/IffSmaqOr0bAwEptTZj10Y/V8cMTK+tguXcfe09AeXIGwpkn2jo5oCwZgYsgoC0ox333jvRy3hsmiv3x+vYEFolV52I6r1rpBm/yZaFI72x5Zqw7gwVjBh82ASKzlOsQM2MzS39Dbt+w94xYfYvkJ+HCerYiS0B7glBZOor0unNIkFAA5uCt4/wfMUtZJu012cMqST+VE1gc/+hcjPQXwYLOaj0oel53LmljNEa+v5Nd51frouT4ntrnxnwTN+b5meX5tZ5AdAYvdGFmZpl3PaO6Z0/xZcrpuU5U4/KT4yE918c3y/1yz03HIx6r++4oDZnl3LFPWxUBU5X9E9n1N1l13LHb6OcxpV7G9Zz6dzmuW8tg0mak32ftUPqUnvS7lE0UR9OzmufXOvXfWpcfQ57vWEF5bCY4cD+gc5mfUQwOAp6b0QiwftcnjufMuh0rGgCH6xvbfG4+W+rHd0NEmRTHf6NnbvmFsP87Ojue70NZ6tDGLfWkI8+zue6q75SWai490rfG9aPl+qmVh7xPaS+hbUyemVMiTF0leT7bv9XDL+ai3JQTWCtP2yfTyjENXjoKGXxkFI8i3Y7lQ+hl9MDabLavvlE6zlPqd1HlJ97s4iOSUxyKnp0PFU2Lq+68ehYXZuT/ynJcm+uFI39pV0mW+U092lZ3jhSeR/0+Y3x+5JmD3yVrAS3C9jTg+TchN9Q6l0msj8CorPodGmEgDV7RcOSD2XPkNtqmDF9pmL5RkRYwbykBrGf5d4nKtIQlb0UDkOUZ29FI8t1yW0q9jO07K0PdMRK8T0V68BT9Ls2gRwZOQcKh4/lfQwZvv5gHKvIjo4aj5fiLJkDiCu0uENIjs08BVZTxSq3HTEPdU3oy0p2q90MeoQxH0uvovTEdUU5pUKQF5//RNLh0HGMLuDvHs/9Dacgs50K+T3mfEvomd1yudSwl+XnSj8cIGDPYOVE6Q629V8Bw2DruHUdMK3kEnculw2E6p5wSEvC8jAbCWrcxNidPGd7zTMxcLzaS6FzMPbb1fDmXqoNz1d2do4wZDYDd36BqpTtyGKfquGV73b02184d9585bODk0/3JO7kz6a4s5ejSgd46rh1cyqmfE8W/BAiXEWC/kjuVlU/9PB8ZDYC1UjOGe1MHJ/a32zb7jjLEKu6T4SiTrzMYk5TCxdXJjvr9s7v+Bn0f7OpEX6YLES7Bhg3c3Z/uPfdK+z533B88cXDkJd/OvbVe2P9hZRQJQ7iMAutRwmT1yn4yGgB3N/qaH6Y9XVnuF+VkHpiPoCx5HDiceoR3w3pEJ0dmynfmHgyol2aZ9qZMypRJ/j9yvzKlFC5dQvLEIy7jsl0Q9J41sXsglFnS+oRL1uP5rj618zexu33/4J7fL7tnhk0OvRrNgIp4fvCGYf1x5x1H3wbuWxpbjXDhsGW+Mwe0P9ZCJmS0aDMHDcU7wuM4QfeEI48QpHPwjn7Z/y22SSZczH0nz30HHsHQgN0DsV6m0Gx/L6666mqLe+pJde/XmN/E3e07owGw/3uDcBkD9n9QGfWAtyVcfMtaZ45se+z++JpcNdIfOIw8ogy+ZYifsNx/CrhNBEuMMvc1hy/9pRYuofUhacTvIqMEsB5w2H7zqWd+Z0teuSOtq+M90wDYLSy+OdIfHOn3NBBTv2d2c8B+LmBy2O0r0OTTLt4r+R3593HPG/9n5EfKcaRATJmvA8rxAlMvWUBSCZ/yQIGYtO9oBhr1UXqSKnr2ExKhIAOFnHsune20T8wXy6WMI2dK7LaoLSiOIKsyF6YuC8ultmf9U+dPdss0MZve00BM/Xa2KQgXMAc+O/ty12NLW9Pgv3uSPZmkcpgJ/kOfD9F0An9SPFlAGjHJLyiSnfbD+EIz0BAwoc+vw+7ITFxGxyfut5zs8j2J9fOw+Sz1CWNT0HBs7VtZ6iYjO4MEXBPTDgvXdQgXMAc+/w+fgOii8Fx/3frbxZAP0eVv0UWIX8yQMh1pJnba2TEn7bclQqaMuD2jZ0FzCJ3NdHR+7ymQjhlA7ODhcZcmPljpOB8qXApKi/NbXc1OlGBTKM/1X7m/z47yXL/i52B9XTzuBmyCt9OBFuXDi/HUV57rgwIYSmdblamkgV7yQzDlz+V/1mFE6sCgKuD2uqMXgxlZrgwxLRYhkLXzkZlr4AzQNsspYpYlDbHph+bzm+XcGAEw5Rv5bLswlnApqVtC/k3gkvF17DmNG+tOOimfp3qKzkDySClcUnw3f9GMwqWJEd5PAtzoQTLq3n2yRt6fKP9/rfLwzeQK0jPI9sj+I4WN4m3vb5blxUiU5VzyfleEVfUebPU7jnAx6+VHAqmJUkQuER7ZOzsQRf4wGCUNp6Q4FKXNb6w8kmNmAnLUJt8Z6VlDl3AWq9T/ds1gzAxSZi/tUCWi2L/adYTidxhY9NIHzoCynItdpg3FKlx+If8DFYGl4IuP9P+0fJYgXARfOVLUZeqPOUV+JS0c6fBlVtPQ0XTNTj4H6GBcwueWurHFzioItCltJ0W4+Kb/nwnMirEpF4Wm8iQtCIQQ0klPFeBxapYi3IMwhgB70kKmdCS79eQh77uwXPItWWaWc8msrbaOCBffOtxrHjlUA9Cw3bNfhHvIXh6Pu8Q7MY7BCArFPpTkH8H/m4ajKA6f0EuxLKpohXj8h0L0WjahoNjhtGmMDVTrdLGQ9huCrZ7GGlgo20nRuYhCzWeal5E2A5RZzljrdkvm3USNSgSJon70tmyagZK6f6c4ZxU0EmYd3rfsNShcviFWGFjXrhv8RsNJkccsGOWxTX8iM/vXXRZcxlLOVr8imArLLWtV5NfYfuuvNA7KdvJfptILCnPgWr1CecOsabpeUrdwuZpAmJee6zHmqi5i9xDxWXK99imhu2goytdMQXZdiCL/Er9NMMlqwYdmnZpVmptWurUo8mukLlTrXEaJ4Y5wPbUTJdYR182nFU3XBd9SbKqNnbrwdURCjBnxTwRGAGjjK5MIB58SuosbWj+uOgpZ8rlz3Je3zmWWdGuatQh/Wc4pTr8durNNPQkXMzqDgFknX1LECpoY3xJexv0Cpj59PJ5DmeRFQJb5gI+xTxDREIH3vm/d0IRbYbM7yGpOA+iYtXlncx2K/fZgxlZPR1oXrraUUVqcA8F/wr+YDmpt0vnS+WLMNVeFGcz4OoM+VoqngKMug6ujaXPg+OjM0jkpiiSwXmSk/bWHv9BX6q/P64PrdwxaWu8IOhm6VGgbRGf1IMIx4+wTR2xWOtpSr8GJjSof0dUr1/VfWgXKCTOYNSCN5sMaBUsDX2ymG/bt0d3AWNUpT7K2tU9IW1ekI/UqX0JjMi7l2FN/QmJWvY4ok8zm7ml6fWnhOP+WhznSZraTobqxjk63Xt5ZuyK/ia0tSd0P3prDtL09xcI9Nj3aOCqy/k6e/DLHfWfPffVOiIomgP1k1BN277fR5tD1e00+Ifu4CLnl/hOHc2DLyNmU4SP32C55QL2wSWd1IhxQJuueIwH3qYi67eU7x+69Yb5RBGzfb+oH27e8jt6Dhe37ufTaR8aSt4qo/6621HuJlMP26DnsPJnkpCV5RpfNq5hpsWlIWUeSa9tIi/Ue3zZzQTGblfQPfS2F+iAtxJPkSPGe7P9Y3bDeQS+0o5EORJSU9e+XEZiYC2cUpsyVpY137ZOsBWTshy9lqNe0FQ1YbqrKtLOUKaZeappuAor6l0nezyt6WSZfW3jxjZj+wzVKzqv0wTMC1rMdeU+2Gdh1jFWfyUuERrvdSBtrK6jfxVqJiXChlzMgmTVf00CMELEJPGsf5WlL+4DYbLbny3tQnqTHHYVnKi91yHR2KBl1KySl49nTCMSao/YVLksjoEPpw08fWUBdpaAkXeel7aIR6sFh2FNiEy7CRPViI5lwMfd1/Y6gzs0MAKSDtAkWCRPzB0VS5Sk6qJuApFEDS5P3kRYiXMw9Xe9A7vnkE6BGIMs3IsLqqnW/DGra7+YYHLhyFx9iOinsXwo6r6HDBi+QjkF0AorGQRr+H54OYk96FpRaL1FQfwEhs6yQEWIsBU0rtOR3yPu1DUz3ZnZTkN4XRN5Vaa7Ju1DUHSVZ0n6gfog+widcVqfId9DVluTcwSyT2VYH5Lo43t6Q/R3K9ztpyH0AgjDe8jKik5Fk6s5dPpJr38DIlEE+kpSduXRe8rFm1APjjX6duEwySyhpQuHS+B0uazU5l1P8Fgshg4auchXsdx7fhPVsYFuS87cU50clS4YPrkktdqIEs2OCE76hflsDuxCB8iZ0xm06KSlDig5FlhliPlIru/D950P4MJc/lHkHqX6HEPVuO+ja8bTc0kpIoy0VNJySdP0fuxJBuIDFYDpk6eAL6k9tpv0mdlRrQr3npJcRSoqnJD1T2lMijOD1hZ3voiBdpjuakcbv6Fu3grxbqYfrRMtVR3L7x/St78Vi3oEImCHvQAaAQYIdwgUsCmm05gOQQ2YRZcBttUOkrL+/GtqRyois0REWAbdIGlkieDXWaHf3HHZeyhQyYi9Id8KLMh5p1K0sQ4a83/a73aeymOxwpJXz32ij9GjfJWlhK/V/G1r/wdZiMVRrcHvqtuyymoV68syp2/knOs+x2Iq12FIwxhzNQ3ikZ5PgcjeiibaxlHnden5pnl+4nh1i5rxzWIsFlEmZsjStOEtzTGqyPpRG/V5R67fsRlaos/ZjUa3Ti+lLpsDTvh9878DR3x3HUuiXdNnMabK9OUzjLmkmduGhYtqM1g4adVLQyhlQv4MwA1ZlubS5JbEuxqr/uZbFbmijmBGlz+ppNaNKMAjluV4SmBNbqJdi7NnSpTDWzKXwXJewBLdzKxlH4q0vwW5mnyGgMcsBXbOLx4HLS797rpcEZsEMAjPLpU2YH28a9sc0+sHuCKe2/HJPfoODsQ2lKsNb9pMkxhAYTkCb+kE9YXcMqCZbHFytAtZx4tpExxEDzhhyhzGXxXwjABkx3rMOrqdoxbDeJ0SE2zEgOUZGy8E3g3yaYVM/QsLJbNYiaQVklnMFgeVjOtwpOTjKsSTOvHJBujU4LGrwW4qAw2awGCXPBLtnrIpANDz1zMWY2mKU/jNfoCxcHCFRAY4cMMPm5/1cjuSnIDAXtlklFPmJGcXPpYa1wnTMoIRNXCHVx4js2wdrtFkwL0ZguAIrtqmd6/6mn5fUFOngfnlgPiWl8zIHEbDW895bLkmcMixT9oAn9nN5ohUQ8JJ9P0rSHudgYZigfjJ7Cdk8SdpwTsP5E4JlNmyzlhKCJT2j+7k0gtZdqm9HSRilLhoTWqWgafi2URP8xWNmqbnlUsqAqcAwiROlETASkLCky6IgCJa1IDPssf2PJP+LCSuyQDLHecxaRmAyD/1WdNeStk0dmReCZSUYZ8k6WOYYSL7Xa4r5tUFsS59b2RAM1BhzwK+szXNTMLcp8pm1Y1ZGYNWwbptnToOYOvf1lQGJYLdrREZgEOwwRZ5tJ0qz0cyRnguoPLdIPLLPFM/oFloY+WyLum2yDmwoPi4ZxSMzFFnLv8NsZRHYfJU2tSHY0hjVFDklvKKQ+2Bb8HMw0oy0ybEtJlkd/v/JTBmdFrgU2B6j73G2mQsAa6ER3h6KXwBamJn5i9k5dqIEAACQHAgXAAAAycGyGD2tGYqxQFcEAcQdAgCACCBcNBISIuu4LoYCRwIAABAElsUAAAAkB8IFAABAciBcAAAAJAfCBQAAQHK2JFxuqD/Kc70kAAAAwaxJuPjCoV/1CRBY3SORUpUnWUkAAAC2iYkw6+NjQBDMf/Y7D8jvTAAAAKJYTeBKoeroZQe/94HJS891RWEgICYAAESyNuGiqj9TzyRewTsfAADiWJVC33TyU+53/QmCBQAA4lnVzEUwewfcU/iyVl/qbZkBAABEsjpT5MZe5yWNR2meAQAA4JIQ/Qun2+e8yYkDrM0AAABsmEoQ7DkNYua8JwAAAEBgPYvJzawjVqCcquOWtS4HAABAAlan0A/BLGupjiRPez7DEgwAAMbhf4M8/zrDY8yKAAAAAElFTkSuQmCC";
    private const string LegalSynqBrandIconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAHgAAAB4CAYAAAA5ZDbSAAAFH0lEQVR42u2d23HiMBSGU4JLcAmOSWZ4pIR0sDxsAYLdfQ4dLB2QDrYEtgOX4AZCVILXwmJyWRsdCev+/zPnFbA+jq7Wf+7uIAiCvIqzVcG39fq0XRzeNnXbR0cI/rZZHE/bmnG2LNGKocLd1s8DLBLUyRB/DoAOKmuXZZ+Bza1gv2a1yGi0bhBwyV2xdoheAa2cKNz3LhuZ7EViYmQb7qW7xpjsWK/9TNkRXBmLI1rdbfY2bgH34zF7rNDy7sZe9djJ6p2qa+WsKjg79wYtYSzeofUdqAfHVDBee2h6f5qKMGFDN+0G8KbeK5Y2B7Oe4WGlmmyh9QOYPZsua0R3rR6HqwIEPAMWM2zzz1YCxnIJgCEAhgA4610ssSadils2JAA4+d4BgAEYAmAIgPNS13VFH099sD52DuLJBHCvveJzxe9fiecB1QHsuo9j514HQ8A6Es+1zhWs+Je3nT+5AHyReM4yJ7jPnX+5BHzRcw5wD10Y8gE4bciBZK5vwEIs1TG3A+CzeHJjsucJVWiAzzPs1JZCOksL1Voz1HXwXnPJt0oF8JEI1nu3NcdOlngOYo+1T2WHSqUmta1KIuQ2l8lVmRpgjWcvYge8jmmyMfdhg5wxX1MVO2BmMptNCHCT9ERLzi5zBnwE4LQBV3IsnooCgCMGnMPhAgADMAADMADnA/hsdsaWpY1QAv75sLL13apwAoz/eqwGJ7ma/Xej4MfiyQVg9/4cQcXZlU+0wWzQpT2g0kVu7OI1AFuN9mYfLx17QAD2CFo3m0XW6vpQAbBvyMRLdxJuY2D7B8AxZLK04u0AOMZQOAHd0ngAHEaIpdy1hX4LwIlmMcEn6oOX8uLwNfiI+VhsO1khS+yiCfc+mu3iyI4bZewVJmQ6XlEAbAO0ugcb9SlRzZxNHOQA2NaeucIMrk/EsfGXGw/eAOxUKr/O0WS0cZKSO2B5qX19JYze6FBNNAHYHWAr72QBMAADMAADMAADMABfXHSmggFwxIBzly3ARmUNbGx0ALAdwKpDodEKMeo3OBYNkPkFLN+POxgdGarS/nIUhTJv9gGLN1TF26sf47R9+E08zuUT/w7ycWE3HEyIjM8rXN0unPtcXqObzvxtiRgAX/uNelkMwKEBJpXfo741AMBhAT5t7l/oC+nN4gVA4wGsBReZHBVgLg7/jaf51NKrAOwcMJfvx82zZB1AY4btCnA/RO7H3lwd6kKJK6wWi2SKOzDDl/gJ9dBx/9fWd4eyVZm0jE5SEtuLBmAABmAABmAABuDMAfu4XQjAbh9wnTngJnfAPFXA0vVdpej9oikPyRIFrKwTlcSkiOB6znX+yTEA7p/nWzaldWS5GVLZN0r9hpABSw9oavm+dSqAZ696ZgI4sMJcaVUk7R/mBYA/aZfU5oSsn9QCcCL1kiYauCJMuFIHnFbXbAtypIDThvtlbdxmBvjY5VTm/cM+dZs4YJ76FVnqduYfHdiBA27lWnh1B0124ZPRfK+YIeDSdoDeDIrpNAkCYAiAARiAAZimsZv1nwLuBnEDVl3PGfVhhgAYAmAIgAEYgHMAPOkTRQJ8/eI13P4cSNxPJrjNMP3PPRfihF9nCKJURBWVUynrVmkP+JtSGxAt70g028V3MIq43UEOmrubrsrQ/Tkgt1l8U5Ac5KDZs7hwZPGEsddzV20TcouuOVnI+vbBkM0xeT7rRWEPiDE31Gw2dOXjQ8bWzKqLHDQ38GV53b1OuPbhEB+CoND1D6mLXlFVwRdjAAAAAElFTkSuQmCC";
    private const string SellerInformationIconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAACwAAAAsCAYAAAAehFoBAAAAmElEQVR42u2YQQ4AIQgD/QL/f+zu3cPGboDa0CaeBJ00gtG1LMtaEfGcDinYK6ClgTPirnCU5nY2bCl0FWwZdOYmFODb1mupcgOPAEYKig6Mti0q8J9eSwP+AjudowEjuQYeASxxhuW6hGQflrvppC+OU2gKMOLiHkMHRvPbgdEN93iaw+g67Q7LPUIln/mjf3/8GWhZ1mC9npUaA0DVsI8AAAAASUVORK5CYII=";
    private const string AssetOverviewIconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAACwAAAAsCAYAAAAehFoBAAAAhElEQVR42u2YQQrAMAgE8wX//9j21lvoUmJcm1nIJQcdRBd0DITQo4i4Zq8VrCW0AmwD/QZUCqxW0qLiq2FTobNg06BXJikBdou3ZdIBVoN/TVgCrPzPhmw7sAJiYWttK9yyh3EJehiXoIdxCVwCl8Alfu8S7ZbQlmv+0dcfjoEIoYN1A/1gCnlwVRmTAAAAAElFTkSuQmCC";
    private const string SupportingDocumentsIconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAACwAAAAsCAYAAAAehFoBAAAAbUlEQVR42u3XwQkAIQwF0bSQ/ovVBjyYEPITmAdedVBZVjMAAAAAT+5+fsaq2DHR0WB5dCRkRHQ0InMipaeUmUQanZ1AFq26l+l1q3aqbZcJ7gxed4cJJpjgpnW7o0v/J0Z/g9XRkteHLBYAdrldSpQUXZpi0gAAAABJRU5ErkJggg==";
    private static readonly Lazy<IReadOnlyList<NotificationEmailInlineAttachment>> ConfirmSaleInlineAttachments = new(BuildConfirmSaleInlineAttachments);

    private readonly ISellingPortfolioRepository _portfolioRepo;
    private readonly ILienRepository _lienRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly IContactRepository _contactRepo;
    private readonly ILienSettlementRepository _settlementRepo;
    private readonly ISettlementPaymentDetailRepository _paymentDetailRepo;
    private readonly IServicingItemRepository _servicingItemRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditPublisher _audit;
    private readonly INotificationPublisher _notifications;
    private readonly ISellingBuyerAccessLinkService _buyerAccessLinks;
    private readonly ILienEligibilityValidator _eligibilityValidator;
    private readonly ILogger<SellingPortfolioService> _logger;

    public SellingPortfolioService(
        ISellingPortfolioRepository portfolioRepo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IContactRepository contactRepo,
        ILienSettlementRepository settlementRepo,
        ISettlementPaymentDetailRepository paymentDetailRepo,
        IServicingItemRepository servicingItemRepo,
        IUnitOfWork unitOfWork,
        IAuditPublisher audit,
        INotificationPublisher notifications,
        ISellingBuyerAccessLinkService buyerAccessLinks,
        ILienEligibilityValidator eligibilityValidator,
        ILogger<SellingPortfolioService> logger)
    {
        _portfolioRepo = portfolioRepo;
        _lienRepo = lienRepo;
        _caseRepo = caseRepo;
        _contactRepo = contactRepo;
        _settlementRepo = settlementRepo;
        _paymentDetailRepo = paymentDetailRepo;
        _servicingItemRepo = servicingItemRepo;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _buyerAccessLinks = buyerAccessLinks;
        _eligibilityValidator = eligibilityValidator;
        _logger = logger;
    }

    public async Task<PaginatedResult<SellingPortfolioResponse>> SearchAsync(
        Guid tenantId,
        Guid? sellerOrgId,
        string? search,
        string? status,
        Guid? buyerOrgId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        if (!string.IsNullOrWhiteSpace(status) && !SellingPortfolioStatus.All.Contains(status))
            throw new ValidationException("One or more fields are invalid.",
                new Dictionary<string, string[]> { ["status"] = [$"Invalid selling portfolio status: '{status}'."] });

        var (items, totalCount) = await _portfolioRepo.SearchAsync(
            tenantId, sellerOrgId, search, status, buyerOrgId, page, pageSize, ct);

        return new PaginatedResult<SellingPortfolioResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<SellingPortfolioResponse?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var entity = await _portfolioRepo.GetByIdAsync(tenantId, id, ct);
        if (entity is not null)
            EnsureSellerPortfolio(entity, sellerOrgId);

        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<SellingPortfolioResponse> CreateAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        CreateSellingPortfolioRequest request,
        CancellationToken ct = default)
    {
        ValidateCreateRequest(request);

        var portfolioNumber = request.PortfolioNumber.Trim();
        var existing = await _portfolioRepo.GetByPortfolioNumberAsync(tenantId, portfolioNumber, ct);
        if (existing is not null)
            throw new ConflictException(
                $"A selling portfolio with number '{portfolioNumber}' already exists.",
                "SELLING_PORTFOLIO_NUMBER_DUPLICATE");

        var portfolio = SellingPortfolio.Create(
            tenantId,
            sellerOrgId,
            portfolioNumber,
            request.Name,
            actingUserId,
            request.Description,
            request.InternalNotes,
            request.TargetGrouping);

        portfolio.AddInitialStatusHistory(actingUserId);

        foreach (var lienId in request.LienIds.Distinct())
        {
            var snapshot = await CreateLienSnapshotAsync(tenantId, sellerOrgId, portfolio.Id, lienId, actingUserId, ct);
            portfolio.AddLien(snapshot, actingUserId);
        }

        foreach (var buyerOrgId in request.BuyerOrgIds.Distinct())
            portfolio.AddBuyer(buyerOrgId, actingUserId);

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.AddAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                "LIEN_SALE_PORTFOLIO_CREATED",
                "SellingPortfolio",
                $"Selling portfolio '{portfolio.PortfolioNumber}' created",
                portfolio.Id.ToString(),
                ct: ct);
        }, ct);

        _logger.LogInformation(
            "Selling portfolio created: {PortfolioId} Number={PortfolioNumber} Tenant={TenantId}",
            portfolio.Id, portfolio.PortfolioNumber, tenantId);

        _audit.Publish(
            eventType: "liens.selling_portfolio.created",
            action: "create",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString());

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        UpdateSellingPortfolioRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["name"] = ["Name is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);
        portfolio.Update(request.Name, actingUserId, request.Description, request.InternalNotes, request.TargetGrouping);
        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                "LIEN_SALE_PORTFOLIO_UPDATED",
                "SellingPortfolio",
                $"Selling portfolio '{portfolio.PortfolioNumber}' updated",
                portfolio.Id.ToString(),
                ct: ct);
        }, ct);

        _audit.Publish(
            eventType: "liens.selling_portfolio.updated",
            action: "update",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString());

        return MapToResponse(portfolio);
    }

    public async Task<AddSellingPortfolioLiensResponse> AddLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioLiensRequest request,
        CancellationToken ct = default)
    {
        var requestedLiens = BuildLienAssignmentRequests(request);
        if (requestedLiens.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["liens"] = ["At least one lien id or code is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var results = new List<AddSellingPortfolioLienResult>();

        foreach (var requestedLien in requestedLiens)
        {
            if (string.IsNullOrWhiteSpace(requestedLien))
            {
                results.Add(FailedLienResult(requestedLien, Guid.Empty, null, "INVALID_LIEN_REFERENCE", "Lien id/code cannot be empty."));
                continue;
            }

            if (portfolio.Status != SellingPortfolioStatus.Draft)
            {
                results.Add(FailedLienResult(
                    requestedLien,
                    TryParseLienId(requestedLien),
                    null,
                    "PORTFOLIO_NOT_EDITABLE",
                    $"Liens can only be added while the portfolio is in '{SellingPortfolioStatus.Draft}'."));
                continue;
            }

            var lien = await ResolveLienForAssignmentAsync(tenantId, requestedLien, ct);
            if (lien is null)
            {
                results.Add(FailedLienResult(requestedLien, TryParseLienId(requestedLien), null, "LIEN_NOT_FOUND", $"Lien '{requestedLien}' was not found."));
                continue;
            }

            var eligibility = await _eligibilityValidator.ValidateAsync(lien, portfolio, ct);
            if (!eligibility.IsEligible)
            {
                LogEligibilityFailure(tenantId, actingUserId, portfolio, lien, eligibility);
                results.Add(FailedLienResult(
                    requestedLien,
                    lien.Id,
                    lien.LienNumber,
                    string.Join(",", eligibility.Violations.Select(v => v.RuleCode)),
                    string.Join(" ", eligibility.Violations.Select(v => v.Message))));
                continue;
            }

            if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            {
                results.Add(FailedLienResult(
                    requestedLien,
                    lien.Id,
                    lien.LienNumber,
                    "SELLER_OWNERSHIP_MISMATCH",
                    $"Lien '{lien.LienNumber}' is not owned by the seller organization."));
                continue;
            }

            string? caseExternalId = null;
            if (lien.CaseId.HasValue)
            {
                var caseEntity = await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct);
                caseExternalId = caseEntity?.ExternalReference;
            }

            var snapshot = SellingPortfolioLien.CreateSnapshot(tenantId, portfolio.Id, lien, caseExternalId, actingUserId);
            portfolio.AddLien(snapshot, actingUserId);

            results.Add(new AddSellingPortfolioLienResult
            {
                RequestedLien = requestedLien,
                LienId = lien.Id,
                LienCode = lien.LienNumber,
                Success = true,
                Status = "added",
            });
        }

        if (results.Any(r => r.Success))
        {
            await InTransactionAsync(async () =>
            {
                await _portfolioRepo.UpdateAsync(portfolio, ct);
                await AddActivityAsync(
                    portfolio,
                    actingUserId,
                    "LIENS_ADDED_TO_PORTFOLIO",
                    "SellingPortfolio",
                    $"{results.Count(r => r.Success)} lien(s) assigned to selling portfolio '{portfolio.PortfolioNumber}'",
                    portfolio.Id.ToString(),
                    $"{{\"addedCount\":{results.Count(r => r.Success)},\"failedCount\":{results.Count(r => !r.Success)}}}",
                    ct);
            }, ct);

            var addedLienIds = string.Join(",", results.Where(r => r.Success).Select(r => r.LienId));
            _audit.Publish(
                eventType: "liens.selling_portfolio.liens_added",
                action: "assign_liens",
                description: $"{results.Count(r => r.Success)} lien(s) assigned to selling portfolio '{portfolio.PortfolioNumber}'",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "SellingPortfolio",
                entityId: portfolio.Id.ToString(),
                metadata: $"{{\"addedLienIds\":\"{addedLienIds}\",\"requestedCount\":{requestedLiens.Count},\"failedCount\":{results.Count(r => !r.Success)}}}");
        }

        return new AddSellingPortfolioLiensResponse
        {
            PortfolioId = portfolio.Id,
            RequestedCount = requestedLiens.Count,
            AddedCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Results = results,
            SuccessfulAssignments = results.Where(r => r.Success).ToList(),
            FailedAssignments = results.Where(r => !r.Success).ToList(),
            Portfolio = MapToResponse(portfolio),
        };
    }

    public async Task<RemoveSellingPortfolioLiensResponse> RemoveLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        RemoveSellingPortfolioLiensRequest request,
        CancellationToken ct = default)
    {
        if (request.LienIds.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["lienIds"] = ["At least one lien id is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var results = new List<RemoveSellingPortfolioLienResult>();
        var removed = new List<SellingPortfolioLien>();

        foreach (var lienId in request.LienIds)
        {
            if (lienId == Guid.Empty)
            {
                results.Add(FailedRemoveLienResult(lienId, null, "INVALID_LIEN_ID", "Lien id cannot be empty."));
                continue;
            }

            if (portfolio.Status != SellingPortfolioStatus.Draft)
            {
                results.Add(FailedRemoveLienResult(
                    lienId,
                    null,
                    "PORTFOLIO_NOT_EDITABLE",
                    $"Liens can only be removed while the portfolio is in '{SellingPortfolioStatus.Draft}'."));
                continue;
            }

            var portfolioLien = portfolio.Liens.FirstOrDefault(l => l.LienId == lienId);
            if (portfolioLien is null)
            {
                results.Add(FailedRemoveLienResult(
                    lienId,
                    null,
                    "LIEN_NOT_IN_PORTFOLIO",
                    $"Lien '{lienId}' is not assigned to this portfolio."));
                continue;
            }

            portfolio.RemoveLien(lienId, actingUserId);
            removed.Add(portfolioLien);

            results.Add(new RemoveSellingPortfolioLienResult
            {
                LienId = portfolioLien.LienId,
                LienCode = portfolioLien.LienNumber,
                Success = true,
                Status = "removed",
            });
        }

        if (removed.Count > 0)
        {
            await InTransactionAsync(async () =>
            {
                await _portfolioRepo.UpdateAsync(portfolio, ct);
                await AddActivityAsync(
                    portfolio,
                    actingUserId,
                    "LIENS_REMOVED_FROM_PORTFOLIO",
                    "SellingPortfolio",
                    $"{removed.Count} lien(s) removed from selling portfolio '{portfolio.PortfolioNumber}'",
                    portfolio.Id.ToString(),
                    $"{{\"removedCount\":{removed.Count},\"failedCount\":{results.Count(r => !r.Success)}}}",
                    ct);
            }, ct);

            foreach (var removedLien in removed)
            {
                _audit.Publish(
                    eventType: "liens.selling_portfolio.lien_removed",
                    action: "LIEN_REMOVED_FROM_PORTFOLIO",
                    description: $"Lien '{removedLien.LienNumber}' removed from selling portfolio '{portfolio.PortfolioNumber}'",
                    tenantId: tenantId,
                    actorUserId: actingUserId,
                    entityType: "SellingPortfolioLien",
                    entityId: removedLien.Id.ToString(),
                    metadata: $"{{\"portfolioId\":\"{portfolio.Id}\",\"lienId\":\"{removedLien.LienId}\",\"lienCode\":\"{removedLien.LienNumber}\"}}");
            }
        }

        return new RemoveSellingPortfolioLiensResponse
        {
            PortfolioId = portfolio.Id,
            RequestedCount = request.LienIds.Count,
            RemovedCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Results = results,
            Portfolio = MapToResponse(portfolio),
        };
    }

    public async Task<SellingPortfolioResponse> AddBuyersAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioBuyersRequest request,
        CancellationToken ct = default)
    {
        if (request.BuyerOrgIds.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["buyerOrgIds"] = ["At least one buyer organization id is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        foreach (var buyerOrgId in request.BuyerOrgIds.Distinct())
            portfolio.AddBuyer(buyerOrgId, actingUserId);

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                "BUYERS_ADDED_TO_PORTFOLIO",
                "SellingPortfolio",
                $"{request.BuyerOrgIds.Distinct().Count()} buyer organization(s) added to selling portfolio '{portfolio.PortfolioNumber}'",
                portfolio.Id.ToString(),
                ct: ct);
        }, ct);
        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> TransitionStatusAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        TransitionSellingPortfolioStatusRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["status"] = ["Status is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);
        var fromStatus = portfolio.Status;
        portfolio.TransitionStatus(request.Status.Trim(), actingUserId, request.Notes);
        var toStatus = portfolio.Status;

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                ResolveStatusActivityAction(toStatus),
                "SellingPortfolio",
                $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {fromStatus} to {toStatus}",
                portfolio.Id.ToString(),
                $"{{\"fromStatus\":\"{fromStatus}\",\"toStatus\":\"{toStatus}\"}}",
                ct);
        }, ct);

        _audit.Publish(
            eventType: "liens.selling_portfolio.status_changed",
            action: "transition",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {fromStatus} to {toStatus}",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString(),
            metadata: $"{{\"fromStatus\":\"{fromStatus}\",\"toStatus\":\"{toStatus}\"}}");

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> PublishAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        string? notes = null,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var transitions = new List<(string FromStatus, string ToStatus, string? Notes)>();

        if (portfolio.Status == SellingPortfolioStatus.Draft)
        {
            var fromStatus = portfolio.Status;
            portfolio.TransitionStatus(
                SellingPortfolioStatus.ReadyForReview,
                actingUserId,
                "Ready for review before publishing");
            transitions.Add((fromStatus, portfolio.Status, "Ready for review before publishing"));
        }

        var publishFromStatus = portfolio.Status;
        portfolio.TransitionStatus(SellingPortfolioStatus.Published, actingUserId, notes);
        transitions.Add((publishFromStatus, portfolio.Status, notes));

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);

            foreach (var transition in transitions)
            {
                await AddActivityAsync(
                    portfolio,
                    actingUserId,
                    ResolveStatusActivityAction(transition.ToStatus),
                    "SellingPortfolio",
                    $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {transition.FromStatus} to {transition.ToStatus}",
                    portfolio.Id.ToString(),
                    $"{{\"fromStatus\":\"{transition.FromStatus}\",\"toStatus\":\"{transition.ToStatus}\"}}",
                    ct);
            }
        }, ct);

        foreach (var transition in transitions)
        {
            _audit.Publish(
                eventType: "liens.selling_portfolio.status_changed",
                action: "transition",
                description: $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {transition.FromStatus} to {transition.ToStatus}",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "SellingPortfolio",
                entityId: portfolio.Id.ToString(),
                metadata: $"{{\"fromStatus\":\"{transition.FromStatus}\",\"toStatus\":\"{transition.ToStatus}\"}}");
        }

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> WithdrawAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        string? notes = null,
        CancellationToken ct = default)
    {
        return await TransitionStatusAsync(
            tenantId,
            id,
            sellerOrgId,
            actingUserId,
            new TransitionSellingPortfolioStatusRequest
            {
                Status = SellingPortfolioStatus.Withdrawn,
                Notes = notes,
            },
            ct);
    }

    public async Task<IReadOnlyList<SellingPortfolioStatusHistoryResponse>> GetStatusHistoryAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var history = await _portfolioRepo.GetStatusHistoryAsync(tenantId, id, ct);
        return history.Select(MapStatusHistory).ToList();
    }

    public async Task<IReadOnlyList<SellingPortfolioActivityResponse>> GetActivityAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var activity = await _portfolioRepo.GetActivityAsync(tenantId, id, ct);
        return activity.Select(MapActivity).ToList();
    }

    public async Task<SellingPortfolioAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var lienIds = portfolio.Liens.Select(l => l.LienId).Distinct().ToList();
        var payments = await _paymentDetailRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var settlements = await _settlementRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var paymentTotal = payments.Sum(p => p.Amount);
        var scheduledSettlementTotal = settlements.Sum(s => s.Amount);
        var balances = portfolio.Liens.Select(l => l.CurrentBalance ?? 0m).ToList();
        var totalOutstanding = balances.Sum();
        var lienCount = portfolio.Liens.Count;
        var activityCount = (await _portfolioRepo.GetActivityAsync(tenantId, id, ct)).Count;
        var settlementExposure = scheduledSettlementTotal > 0m
            ? Math.Max(scheduledSettlementTotal - paymentTotal, 0m)
            : Math.Max(totalOutstanding - paymentTotal, 0m);

        return new SellingPortfolioAnalyticsResponse
        {
            PortfolioId = portfolio.Id,
            Financial = new SellingPortfolioFinancialSummary
            {
                TotalReceivables = portfolio.Liens.Sum(l => l.OriginalAmount),
                TotalOutstandingBalance = totalOutstanding,
                SettlementExposure = settlementExposure,
                PaymentTotal = paymentTotal,
                AverageLienBalance = lienCount == 0 ? 0m : decimal.Round(totalOutstanding / lienCount, 2),
            },
            AgingBuckets = BuildAgingBuckets(portfolio.Liens),
            Operational = new SellingPortfolioOperationalSummary
            {
                LienCount = lienCount,
                Status = portfolio.Status,
                PublishedAtUtc = portfolio.PublishedAtUtc,
                ClosedAtUtc = portfolio.ClosedAtUtc,
                ActivityCount = activityCount,
            },
            Concentrations = BuildConcentrations(portfolio.Liens),
        };
    }

    public async Task<SendLienBuyerEmailResponse> SendBuyerEmailAsync(
        Guid tenantId,
        Guid portfolioId,
        string lienIdOrCode,
        Guid sellerOrgId,
        Guid actingUserId,
        SendLienBuyerEmailRequest request,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var detailsUrl = request.DetailsUrl.Trim();
        Uri? detailsUri = null;
        if (string.IsNullOrWhiteSpace(lienIdOrCode))
            errors["lienIdOrCode"] = ["Lien ID/code is required."];
        if (request.BuyerContactId == Guid.Empty)
            errors["buyerContactId"] = ["Buyer contact id is required."];
        if (string.IsNullOrWhiteSpace(detailsUrl) ||
            !Uri.TryCreate(detailsUrl, UriKind.Absolute, out detailsUri))
        {
            errors["detailsUrl"] = ["A valid absolute lien details URL is required."];
        }

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var portfolio = await RequirePortfolioAsync(tenantId, portfolioId, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var lien = await ResolveLienAsync(tenantId, lienIdOrCode, ct)
            ?? throw new NotFoundException($"Lien '{lienIdOrCode}' not found for tenant '{tenantId}'.");

        if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            throw new ValidationException("Referenced lien is not owned by the seller organization.",
                new Dictionary<string, string[]> { ["lienIdOrCode"] = [$"Lien '{lienIdOrCode}' is not owned by seller organization '{sellerOrgId}'."] });

        var portfolioLien = portfolio.Liens.FirstOrDefault(l =>
            l.LienId == lien.Id ||
            string.Equals(l.LienNumber, lien.LienNumber, StringComparison.OrdinalIgnoreCase));

        if (portfolioLien is null)
            throw new ValidationException("Referenced lien is not part of the selling portfolio.",
                new Dictionary<string, string[]> { ["lienIdOrCode"] = [$"Lien '{lienIdOrCode}' is not attached to selling portfolio '{portfolioId}'."] });

        var contact = await _contactRepo.GetByIdAsync(tenantId, request.BuyerContactId, ct)
            ?? throw new NotFoundException($"Buyer contact '{request.BuyerContactId}' not found for tenant '{tenantId}'.");

        if (!contact.IsActive)
            throw new ValidationException("Buyer contact is inactive.",
                new Dictionary<string, string[]> { ["buyerContactId"] = ["Buyer contact must be active."] });

        if (string.IsNullOrWhiteSpace(contact.Email))
            throw new ValidationException("Buyer contact email is required.",
                new Dictionary<string, string[]> { ["buyerContactId"] = ["Buyer contact must have an email address."] });

        if (!portfolio.Buyers.Any(b => b.BuyerOrgId == contact.OrgId))
            throw new ValidationException("Buyer contact is not associated with this selling portfolio.",
                new Dictionary<string, string[]> { ["buyerContactId"] = [$"Buyer contact '{request.BuyerContactId}' does not belong to a buyer organization on portfolio '{portfolioId}'."] });

        var caseEntity = lien.CaseId.HasValue
            ? await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct)
            : null;

        var plaintiffName = BuildPlaintiffName(caseEntity, lien);
        var serviceOrLossDate = (caseEntity?.DateOfIncident ?? lien.IncidentDate)
            ?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? "Unknown date";
        var lienCode = string.IsNullOrWhiteSpace(lien.LienNumber) ? lien.Id.ToString() : lien.LienNumber;
        var subject = $"{plaintiffName} - {serviceOrLossDate} - {lienCode}";
        var body =
            $"Hi {contact.DisplayName}, please find the lien details at the link below:{Environment.NewLine}{Environment.NewLine}" +
            $"{detailsUri!}{Environment.NewLine}{Environment.NewLine}" +
            "Let me know if you have any questions. Thank you.";

        var notificationResult = await _notifications.SendEmailAsync(
            "liens.selling.buyer_lien_details",
            tenantId,
            contact.Email.Trim(),
            subject,
            body,
            new Dictionary<string, string>
            {
                ["tenantId"] = tenantId.ToString(),
                ["portfolioId"] = portfolio.Id.ToString(),
                ["lienId"] = lien.Id.ToString(),
                ["lienCode"] = lienCode,
                ["buyerContactId"] = contact.Id.ToString(),
                ["buyerOrgId"] = contact.OrgId.ToString(),
                ["requestedBy"] = actingUserId.ToString(),
            },
            ct);

        if (!notificationResult.Succeeded)
        {
            throw new ServiceUnavailableException(
                notificationResult.LastErrorMessage
                ?? $"Buyer lien email was not sent. Notification status: {notificationResult.Status}.");
        }

        _audit.Publish(
            eventType: "liens.selling.buyer_lien_email_sent",
            action: "send_email",
            description: $"Buyer lien details email sent for lien '{lienCode}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: lien.Id.ToString(),
            metadata: $"{{\"portfolioId\":\"{portfolio.Id}\",\"buyerContactId\":\"{contact.Id}\"}}");

        return new SendLienBuyerEmailResponse
        {
            Success = true,
            NotificationId = notificationResult.NotificationId,
            NotificationStatus = notificationResult.Status,
            LienId = lien.Id,
            LienCode = lienCode,
            BuyerContactId = contact.Id,
            BuyerOrgId = contact.OrgId,
            BuyerName = contact.DisplayName,
            BuyerEmail = contact.Email.Trim(),
            Subject = subject,
            Body = body,
        };
    }

    public async Task<ConfirmSellingLienSaleResponse> ConfirmSaleAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid actingUserId,
        ConfirmSellingLienSaleRequest request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        if (!request.ConfirmationAccepted)
        {
            throw new ValidationException("Sale confirmation must be accepted.",
                new Dictionary<string, string[]>
                {
                    ["confirmationAccepted"] = ["Confirm the sale before submitting it."],
                });
        }

        var lien = await _lienRepo.GetByIdAsync(tenantId, lienId, ct)
            ?? throw new NotFoundException($"Lien '{lienId}' not found for tenant '{tenantId}'.");

        if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            throw new ValidationException("Referenced lien is not owned by the seller organization.",
                new Dictionary<string, string[]> { ["lienId"] = [$"Lien '{lienId}' is not owned by seller organization '{sellerOrgId}'."] });

        if (lien.Status != LienStatus.Draft &&
            !(lien.Status == LienStatus.Offered &&
              string.Equals(lien.SellerStatus, SellingLienStatus.SubmittedForSale, StringComparison.Ordinal)))
        {
            throw new ValidationException("Lien cannot be confirmed for sale from its current status.",
                new Dictionary<string, string[]>
                {
                    ["status"] = [$"Only draft or already submitted-for-sale liens can be confirmed. Current status: '{lien.Status}'."],
                });
        }

        if (!lien.AskAmount.HasValue || lien.AskAmount.Value <= 0m)
        {
            throw new ValidationException("Ask amount is required before confirming sale.",
                new Dictionary<string, string[]>
                {
                    ["askAmount"] = ["A positive AskAmount is required before confirming sale."],
                });
        }

        ConfirmSaleNotificationContext? notificationContext = null;
        string? notificationIdempotencyKey = null;
        if (request.SendBuyerNotification)
        {
            notificationContext = await BuildConfirmSaleNotificationContextAsync(
                tenantId,
                sellerOrgId,
                actingUserId,
                lien,
                ct);

            notificationIdempotencyKey = BuildConfirmSaleNotificationIdempotencyKey(
                tenantId,
                lien.Id,
                notificationContext.BuyerContact.Id,
                idempotencyKey);
        }

        SellingBuyerAccessLinkResult? accessLink = null;
        await InTransactionAsync(async () =>
        {
            if (lien.Status == LienStatus.Draft)
                lien.ListForSale(lien.AskAmount.Value, actingUserId);

            await _lienRepo.UpdateAsync(lien, ct);

            if (notificationContext is not null)
            {
                accessLink = await _buyerAccessLinks.CreateOrGetForConfirmSaleAsync(
                    tenantId,
                    lien.Id,
                    sellerOrgId,
                    notificationContext.BuyerContact.OrgId,
                    notificationContext.BuyerContact.Id,
                    actingUserId,
                    notificationIdempotencyKey!,
                    TimeSpan.FromDays(30),
                    ct);
            }
        }, ct);

        ConfirmSellingLienBuyerNotificationResponse? notification = null;
        if (notificationContext is not null && accessLink is not null)
        {
            notification = await SendConfirmSaleNotificationAsync(
                tenantId,
                actingUserId,
                lien,
                notificationContext,
                accessLink,
                notificationIdempotencyKey!,
                ct);
        }

        _audit.Publish(
            eventType: "liens.selling.confirm_sale",
            action: "confirm_sale",
            description: $"Lien '{lien.LienNumber}' confirmed for sale",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: lien.Id.ToString(),
            metadata: $"{{\"sendBuyerNotification\":{request.SendBuyerNotification.ToString().ToLowerInvariant()}}}");

        return MapConfirmSaleResponse(lien, notification);
    }

    private async Task<ConfirmSaleNotificationContext> BuildConfirmSaleNotificationContextAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        Lien lien,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (!lien.FundingCompanyId.HasValue || lien.FundingCompanyId.Value == Guid.Empty)
            errors["fundingCompanyId"] = ["FundingCompanyId is required before sending the buyer notification."];

        if (!lien.FundingCompanyContactId.HasValue || lien.FundingCompanyContactId.Value == Guid.Empty)
            errors["fundingCompanyContactId"] = ["FundingCompanyContactId is required before sending the buyer notification."];

        if (!lien.InitialServiceDate.HasValue)
            errors["initialServiceDate"] = ["InitialServiceDate is required before sending the buyer notification."];

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var buyerContact = await _contactRepo.GetByIdAsync(tenantId, lien.FundingCompanyContactId!.Value, ct)
            ?? throw new NotFoundException($"Buyer contact '{lien.FundingCompanyContactId.Value}' not found for tenant '{tenantId}'.");

        if (!buyerContact.IsActive)
            errors["fundingCompanyContactId"] = ["Buyer contact must be active."];

        if (buyerContact.OrgId != lien.FundingCompanyId!.Value)
            errors["fundingCompanyContactId"] = ["Buyer contact must belong to the selected funding company."];

        if (string.IsNullOrWhiteSpace(buyerContact.Email))
            errors["fundingCompanyContactId"] = ["Buyer contact must have an email address."];

        var sellerContact = SelectSellerContact(await _contactRepo.GetByOrgIdAsync(tenantId, sellerOrgId, isActive: true, ct));
        if (sellerContact is null)
            errors["sellerContact"] = ["An active seller contact is required before sending the buyer notification."];
        else
        {
            if (string.IsNullOrWhiteSpace(sellerContact.DisplayName))
                errors["sellerContact"] = ["Seller contact must have a display name."];
            if (string.IsNullOrWhiteSpace(sellerContact.Organization))
                errors["sellerCompany"] = ["Seller contact must have an organization/company."];
            if (string.IsNullOrWhiteSpace(sellerContact.Email))
                errors["sellerEmail"] = ["Seller contact must have an email address."];
        }

        var caseEntity = lien.CaseId.HasValue
            ? await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct)
            : null;
        var handlingLawFirm = await ResolveHandlingLawFirmAsync(tenantId, caseEntity, ct);
        if (string.IsNullOrWhiteSpace(handlingLawFirm))
            errors["handlingLawFirm"] = ["A real handling law firm is required before sending the buyer notification."];

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var caseManager = await ResolveCaseManagerAsync(tenantId, caseEntity, ct);
        var documentNames = await GetSupportingDocumentNamesAsync(tenantId, lien, caseEntity, ct);

        return new ConfirmSaleNotificationContext(
            buyerContact,
            sellerContact!,
            caseEntity,
            handlingLawFirm!,
            caseManager,
            documentNames);
    }

    private async Task<ConfirmSellingLienBuyerNotificationResponse> SendConfirmSaleNotificationAsync(
        Guid tenantId,
        Guid actingUserId,
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult accessLink,
        string idempotencyKey,
        CancellationToken ct)
    {
        if (IsSubmittedNotificationStatus(accessLink.NotificationStatus))
        {
            return new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = true,
                Submitted = true,
                NotificationId = accessLink.NotificationId,
                NotificationStatus = accessLink.NotificationStatus,
                BuyerAccessLinkId = accessLink.Id,
                BuyerPortalUrl = accessLink.BuyerPortalUrl,
                ExpiresAtUtc = accessLink.ExpiresAtUtc,
                BuyerContactId = context.BuyerContact.Id,
                BuyerOrgId = context.BuyerContact.OrgId,
                BuyerEmail = context.BuyerContact.Email!.Trim(),
            };
        }

        var email = BuildConfirmSaleEmail(lien, context, accessLink);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString(),
            ["lienId"] = lien.Id.ToString(),
            ["lienCode"] = ResolveLienCode(lien),
            ["buyerContactId"] = context.BuyerContact.Id.ToString(),
            ["buyerOrgId"] = context.BuyerContact.OrgId.ToString(),
            ["sellerOrgId"] = context.SellerContact.OrgId.ToString(),
            ["buyerAccessLinkId"] = accessLink.Id.ToString(),
            ["buyerAccessExpiresAtUtc"] = accessLink.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["requestedBy"] = actingUserId.ToString(),
        };

        try
        {
            var notificationResult = await _notifications.SendEmailAsync(
                NotificationTaxonomy.Liens.Events.SellingLienSubmitted,
                tenantId,
                context.BuyerContact.Email!.Trim(),
                email.Subject,
                email.TextBody,
                metadata,
                ct,
                new NotificationEmailSendOptions(
                    IdempotencyKey: idempotencyKey,
                    TemplateKey: NotificationTaxonomy.Liens.Templates.SellingLienSubmittedEmail,
                    TemplateData: email.TemplateData,
                    RequestedBy: actingUserId.ToString(),
                    BrandedRendering: true,
                    HtmlBody: email.HtmlBody,
                    TextBody: email.TextBody,
                    InlineAttachments: email.InlineAttachments,
                    DisableClickTracking: true));

            await _buyerAccessLinks.MarkNotificationSubmittedAsync(
                tenantId,
                accessLink.Id,
                notificationResult.NotificationId,
                notificationResult.Status,
                ct);

            var submitted = IsSubmittedNotificationStatus(notificationResult.Status) &&
                            !notificationResult.BlockedByPolicy &&
                            string.IsNullOrWhiteSpace(notificationResult.FailureCategory);

            return new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = true,
                Submitted = submitted,
                NotificationId = notificationResult.NotificationId,
                NotificationStatus = notificationResult.Status,
                FailureMessage = submitted ? null : notificationResult.LastErrorMessage,
                BuyerAccessLinkId = accessLink.Id,
                BuyerPortalUrl = accessLink.BuyerPortalUrl,
                ExpiresAtUtc = accessLink.ExpiresAtUtc,
                BuyerContactId = context.BuyerContact.Id,
                BuyerOrgId = context.BuyerContact.OrgId,
                BuyerEmail = context.BuyerContact.Email.Trim(),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Confirm-sale buyer notification failed: Tenant={TenantId} Lien={LienId} BuyerContact={BuyerContactId}",
                tenantId, lien.Id, context.BuyerContact.Id);

            await _buyerAccessLinks.MarkNotificationSubmittedAsync(
                tenantId,
                accessLink.Id,
                null,
                "failed",
                ct);

            return new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = true,
                Submitted = false,
                NotificationStatus = "failed",
                FailureMessage = ex.Message,
                BuyerAccessLinkId = accessLink.Id,
                BuyerPortalUrl = accessLink.BuyerPortalUrl,
                ExpiresAtUtc = accessLink.ExpiresAtUtc,
                BuyerContactId = context.BuyerContact.Id,
                BuyerOrgId = context.BuyerContact.OrgId,
                BuyerEmail = context.BuyerContact.Email!.Trim(),
            };
        }
    }

    private static ConfirmSaleEmail BuildConfirmSaleEmail(
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult accessLink)
    {
        const string subject = "New Lien Offer";
        var lienCode = ResolveLienCode(lien);
        var billingAmount = lien.OriginalAmount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        var initialServiceDate = lien.InitialServiceDate!.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        var sellerName = context.SellerContact.DisplayName.Trim();
        var sellerCompany = context.SellerContact.Organization!.Trim();
        var sellerEmail = context.SellerContact.Email!.Trim();
        var handlingLawFirm = context.HandlingLawFirm.Trim();
        var caseManager = context.CaseManager?.Trim();
        var documentNames = context.DocumentNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToList();

        var templateData = new Dictionary<string, string>
        {
            ["subject"] = subject,
            ["status"] = "Awaiting Your Response",
            ["intro"] = "A medical lien has been submitted to your company for review and potential purchase. Review the asset overview below to proceed.",
            ["sellerName"] = sellerName,
            ["sellerCompany"] = sellerCompany,
            ["billingAmount"] = billingAmount,
            ["initialServiceDate"] = initialServiceDate,
            ["contactPerson"] = sellerName,
            ["emailAddress"] = sellerEmail,
            ["handlingLawFirm"] = handlingLawFirm,
            ["lienCode"] = lienCode,
            ["buyerPortalUrl"] = accessLink.BuyerPortalUrl,
            ["expiresAtUtc"] = accessLink.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(caseManager))
            templateData["caseManager"] = caseManager;

        if (documentNames.Count > 0)
            templateData["supportingDocuments"] = string.Join(", ", documentNames);

        var sellerRows = new (string Label, string? Value)[]
        {
            ("Seller Name", sellerName),
            ("Seller Company", sellerCompany),
        };

        var assetRows = new (string Label, string? Value)[]
        {
            ("Billing Amount", billingAmount),
            ("Initial Service Date", initialServiceDate),
            ("Contact Person", sellerName),
            ("Email Address", sellerEmail),
            ("Handling Law Firm", handlingLawFirm),
            ("Case Manager", caseManager),
        };

        var htmlBody = new StringBuilder();
        htmlBody.AppendLine("<!doctype html>");
        htmlBody.AppendLine("<html lang=\"en\">");
        htmlBody.AppendLine("<head>");
        htmlBody.AppendLine("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        htmlBody.AppendLine("<meta name=\"color-scheme\" content=\"light only\"><meta name=\"supported-color-schemes\" content=\"light only\">");
        htmlBody.AppendLine("<title>New Lien Offer</title>");
        htmlBody.AppendLine("<style>");
        htmlBody.AppendLine(":root{color-scheme:light only;supported-color-schemes:light only;}");
        htmlBody.AppendLine("body,table,td,p,a,span{color-scheme:light only;supported-color-schemes:light only;}");
        htmlBody.AppendLine(".email-bg{background-color:#f4f5f7 !important;}.email-shell{background-color:#ffffff !important;}.email-card{background-color:#ffffff !important;color:#111827 !important;}");
        htmlBody.AppendLine(".email-label{color:#6f6f6f !important;}.email-value{color:#111111 !important;}.email-rule{border-color:#e5e5e5 !important;}");
        htmlBody.AppendLine("@media (prefers-color-scheme: dark){.email-bg{background-color:#f4f5f7 !important;}.email-shell,.email-card{background-color:#ffffff !important;color:#111827 !important;}.email-label{color:#6f6f6f !important;}.email-value{color:#111111 !important;}.email-rule{border-color:#e5e5e5 !important;}}");
        htmlBody.AppendLine("[data-ogsc] .email-bg{background-color:#f4f5f7 !important;}[data-ogsc] .email-shell,[data-ogsc] .email-card{background-color:#ffffff !important;color:#111827 !important;}");
        htmlBody.AppendLine("</style>");
        htmlBody.AppendLine("</head>");
        htmlBody.AppendLine("<body class=\"email-bg\" bgcolor=\"#f4f5f7\" style=\"margin:0;padding:0;background-color:#f4f5f7 !important;font-family:Arial,'Helvetica Neue',Helvetica,sans-serif;color:#111827 !important;color-scheme:light only;supported-color-schemes:light only;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#f4f5f7\" class=\"email-bg\" style=\"width:100%;border-collapse:collapse;background-color:#f4f5f7 !important;\">");
        htmlBody.AppendLine("<tr><td align=\"center\" bgcolor=\"#f4f5f7\" class=\"email-bg\" style=\"padding:28px 14px;background-color:#f4f5f7 !important;\">");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"560\" cellspacing=\"0\" cellpadding=\"0\" class=\"email-shell\" bgcolor=\"#ffffff\" style=\"width:100%;max-width:560px;border-collapse:separate;border-spacing:0;background-color:#ffffff !important;border-radius:10px;overflow:hidden;\">");
        htmlBody.AppendLine("<tr><td bgcolor=\"#071b31\" style=\"background-color:#071b31 !important;border-radius:10px 10px 0 0;padding:28px 30px 28px;\">");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;margin:0 0 28px 0;\"><tr>");
        htmlBody.AppendLine("<td align=\"left\" style=\"vertical-align:middle;padding:0;\">");
        AppendLegalSynqEmailBrand(htmlBody);
        htmlBody.AppendLine("</td>");
        htmlBody.AppendLine("<td align=\"right\" style=\"vertical-align:middle;padding:0;\">");
        htmlBody.AppendLine("<span style=\"display:inline-block;background-color:#263127 !important;color:#f3c400 !important;border-radius:999px;padding:6px 12px;font-size:12px;font-weight:600;line-height:1.1;white-space:nowrap;\">Awaiting Your Response</span>");
        htmlBody.AppendLine("</td>");
        htmlBody.AppendLine("</tr></table>");
        htmlBody.AppendLine("<h1 style=\"margin:0 0 10px 0;color:#ffffff !important;font-size:24px;line-height:1.25;font-weight:700;letter-spacing:0;\">New Lien Offer</h1>");
        htmlBody.AppendLine("<p style=\"margin:0;color:#ffffff !important;font-size:16px;line-height:1.55;font-weight:400;opacity:.92;\">A medical lien has been submitted to your company for review and potential purchase. Review the asset overview below to proceed.</p>");
        htmlBody.AppendLine("</td></tr>");
        htmlBody.AppendLine("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"background-color:#ffffff !important;color:#111827 !important;border:1px solid #e5e5e5;border-top:0;border-radius:0 0 10px 10px;padding:24px 24px 28px;\">");
        AppendEmailSection(htmlBody, "Seller Information", sellerRows);
        AppendEmailSection(htmlBody, "Asset Overview", assetRows);

        if (documentNames.Count > 0)
            AppendDocumentsSection(htmlBody, documentNames);

        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;border-spacing:0;margin:4px 0 12px 0;\"><tr>");
        htmlBody.Append("<td align=\"center\" bgcolor=\"#f26a2e\" style=\"background-color:#f26a2e !important;border-radius:8px;\"><a href=\"")
            .Append(Html(accessLink.BuyerPortalUrl))
            .AppendLine("\" style=\"display:block;padding:12px 20px;color:#ffffff !important;text-decoration:none;font-size:13px;font-weight:700;line-height:1.1;\">View Lien for Sale</a></td>");
        htmlBody.AppendLine("</tr></table>");
        htmlBody.AppendLine("<p class=\"email-label\" style=\"margin:0 0 20px 0;text-align:center;color:#7a7a7a !important;font-size:13px;line-height:1.5;\">This Link Expires in 30 Days</p>");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card email-rule\" style=\"border-collapse:separate;border-spacing:0;background-color:#ffffff !important;\"><tr>");
        htmlBody.Append("<td style=\"width:28px;padding:15px 0 15px 14px;vertical-align:top;")
            .Append(EmailTableCellBorder(isFirstRow: true, isLastRow: true, leftEdge: true, rightEdge: false))
            .AppendLine("\"><span style=\"display:inline-block;width:14px;height:14px;line-height:14px;text-align:center;border-radius:50%;border:1px solid #f3c400;color:#f3a800 !important;font-size:10px;font-weight:700;\">i</span></td>");
        htmlBody.Append("<td class=\"email-label\" style=\"padding:14px 16px 14px 8px;color:#6f6f6f !important;font-size:13px;line-height:1.55;")
            .Append(EmailTableCellBorder(isFirstRow: true, isLastRow: true, leftEdge: false, rightEdge: true))
            .Append("\">This offer was sent on behalf of the <strong class=\"email-value\" style=\"color:#111111 !important;font-weight:700;\">")
            .Append(Html(sellerCompany))
            .Append("</strong>. Please reply directly to <a href=\"mailto:")
            .Append(Html(sellerEmail))
            .Append("\" style=\"color:#f26a2e !important;text-decoration:underline;\">")
            .Append(Html(sellerEmail))
            .AppendLine("</a> for any questions.</td>");
        htmlBody.AppendLine("</tr></table>");
        htmlBody.AppendLine("</td></tr>");
        htmlBody.AppendLine("</table>");
        htmlBody.AppendLine("</td></tr>");
        htmlBody.AppendLine("</table>");
        htmlBody.AppendLine("</body></html>");

        var textBody = BuildConfirmSaleTextBody(
            accessLink.BuyerPortalUrl,
            sellerCompany,
            sellerEmail,
            sellerRows,
            assetRows,
            documentNames);

        return new ConfirmSaleEmail(subject, htmlBody.ToString(), textBody, templateData, ConfirmSaleInlineAttachments.Value);
    }

    private static void AppendEmailSection(
        StringBuilder body,
        string title,
        IReadOnlyList<(string Label, string? Value)> rows)
    {
        var visibleRows = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Value))
            .ToList();
        if (visibleRows.Count == 0)
            return;

        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card\" style=\"width:100%;border-collapse:collapse;margin:0 0 28px 0;background-color:#ffffff !important;\">");
        AppendEmailSectionHeading(body, title);
        body.AppendLine("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"padding:0;background-color:#ffffff !important;\">");
        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card email-rule\" style=\"width:100%;border-collapse:separate;border-spacing:0;background-color:#ffffff !important;\">");

        for (var i = 0; i < visibleRows.Count; i++)
        {
            var (label, value) = visibleRows[i];
            var isFirstRow = i == 0;
            var isLastRow = i == visibleRows.Count - 1;
            var labelBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: true, rightEdge: false);
            var valueBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: false, rightEdge: true);

            body.Append("<tr><td bgcolor=\"#ffffff\" class=\"email-card email-label\" style=\"width:44%;padding:15px 14px;color:#6f6f6f !important;background-color:#ffffff !important;font-size:13px;line-height:1.45;")
                .Append(labelBorder)
                .Append("\">")
                .Append(Html(label))
                .Append("</td><td align=\"right\" bgcolor=\"#ffffff\" class=\"email-card email-value\" style=\"width:56%;padding:15px 14px;color:#111111 !important;background-color:#ffffff !important;font-size:15px;line-height:1.45;font-weight:500;")
                .Append(valueBorder)
                .Append("\">");
            AppendEmailValue(body, label, value!.Trim());
            body.AppendLine("</td></tr>");
        }

        body.AppendLine("</table>");
        body.AppendLine("</td></tr>");
        body.AppendLine("</table>");
    }

    private static void AppendEmailValue(StringBuilder body, string label, string value)
    {
        if (string.Equals(label, "Email Address", StringComparison.OrdinalIgnoreCase))
        {
            body.Append("<a href=\"mailto:")
                .Append(Html(value))
                .Append("\" style=\"color:#111111 !important;text-decoration:none;\">")
                .Append(Html(value))
                .Append("</a>");
            return;
        }

        body.Append(Html(value));
    }

    private static void AppendLegalSynqEmailBrand(StringBuilder body)
    {
        body.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" aria-label=\"LegalSynq\" style=\"border-collapse:collapse;\"><tr><td width=\"36\" style=\"width:36px;padding:0 6px 0 0;vertical-align:middle;\"><img src=\"cid:")
            .Append(LegalSynqBrandIconContentId)
            .AppendLine("\" width=\"36\" height=\"36\" alt=\"\" role=\"presentation\" style=\"display:block;width:36px;height:36px;border:0;outline:none;text-decoration:none;\"></td><td style=\"padding:0;vertical-align:middle;white-space:nowrap;\"><span style=\"color:#ffffff !important;-webkit-text-fill-color:#ffffff;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Legal</span><span style=\"color:#f26a2e !important;-webkit-text-fill-color:#f26a2e;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Synq</span></td></tr></table>");
    }

    private static void AppendEmailSectionHeading(StringBuilder body, string title)
    {
        body.Append("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"padding:0 0 13px 0;background-color:#ffffff !important;\"><img src=\"cid:")
            .Append(EmailSectionIconContentId(title))
            .Append("\" width=\"22\" height=\"22\" alt=\"\" role=\"presentation\" style=\"display:inline-block;width:22px;height:22px;border:0;outline:none;text-decoration:none;vertical-align:middle;\"><span style=\"display:inline-block;margin-left:8px;color:#111111 !important;-webkit-text-fill-color:#111111;font-size:16px;font-weight:600;line-height:22px;letter-spacing:0;vertical-align:middle;\">")
            .Append(Html(title))
            .AppendLine("</span></td></tr>");
    }

    private static string EmailSectionIconContentId(string title)
        => title switch
        {
            "Seller Information" => SellerInformationIconContentId,
            "Asset Overview" => AssetOverviewIconContentId,
            "Supporting Documents" => SupportingDocumentsIconContentId,
            _ => AssetOverviewIconContentId,
        };

    private static void AppendDocumentsSection(
        StringBuilder body,
        IReadOnlyList<string> documentNames)
    {
        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card\" style=\"width:100%;border-collapse:collapse;margin:0 0 24px 0;background-color:#ffffff !important;\">");
        AppendEmailSectionHeading(body, "Supporting Documents");
        body.AppendLine("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"padding:0;background-color:#ffffff !important;\">");
        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card email-rule\" style=\"width:100%;border-collapse:separate;border-spacing:0;background-color:#ffffff !important;\">");

        for (var i = 0; i < documentNames.Count; i++)
        {
            var isFirstRow = i == 0;
            var isLastRow = i == documentNames.Count - 1;
            var iconBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: true, rightEdge: false);
            var valueBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: false, rightEdge: true);
            body.Append("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"width:32px;padding:15px 0 15px 14px;background-color:#ffffff !important;")
                .Append(iconBorder)
                .Append("\"><span style=\"display:inline-block;width:18px;height:18px;line-height:18px;text-align:center;border-radius:5px;background-color:#f26a2e !important;color:#ffffff !important;font-size:12px;font-weight:700;\">&#10003;</span></td><td align=\"right\" bgcolor=\"#ffffff\" class=\"email-card email-value\" style=\"padding:15px 14px 15px 8px;color:#111111 !important;background-color:#ffffff !important;font-size:15px;line-height:1.45;font-weight:500;")
                .Append(valueBorder)
                .Append("\">")
                .Append(Html(documentNames[i]))
                .AppendLine("</td></tr>");
        }

        body.AppendLine("</table>");
        body.AppendLine("</td></tr>");
        body.AppendLine("</table>");
    }

    private static string EmailTableCellBorder(bool isFirstRow, bool isLastRow, bool leftEdge, bool rightEdge)
    {
        var border = new StringBuilder();

        if (isFirstRow)
            border.Append("border-top:1px solid #e5e5e5;");

        border.Append("border-bottom:1px solid #e5e5e5;");

        if (leftEdge)
        {
            border.Append("border-left:1px solid #e5e5e5;");
            if (isFirstRow)
                border.Append("border-top-left-radius:10px;");
            if (isLastRow)
                border.Append("border-bottom-left-radius:10px;");
        }

        if (rightEdge)
        {
            border.Append("border-right:1px solid #e5e5e5;");
            if (isFirstRow)
                border.Append("border-top-right-radius:10px;");
            if (isLastRow)
                border.Append("border-bottom-right-radius:10px;");
        }

        return border.ToString();
    }

    private static IReadOnlyList<NotificationEmailInlineAttachment> BuildConfirmSaleInlineAttachments()
        =>
        [
            new(
                LegalSynqBrandIconContentId,
                "legalsynq-brand-icon.png",
                "image/png",
                LegalSynqBrandIconPngBase64),
            new(
                SellerInformationIconContentId,
                "seller-information-icon.png",
                "image/png",
                SellerInformationIconPngBase64),
            new(
                AssetOverviewIconContentId,
                "asset-overview-icon.png",
                "image/png",
                AssetOverviewIconPngBase64),
            new(
                SupportingDocumentsIconContentId,
                "supporting-documents-icon.png",
                "image/png",
                SupportingDocumentsIconPngBase64),
        ];

    private static string BuildConfirmSaleTextBody(
        string buyerPortalUrl,
        string sellerCompany,
        string sellerEmail,
        IReadOnlyList<(string Label, string? Value)> sellerRows,
        IReadOnlyList<(string Label, string? Value)> assetRows,
        IReadOnlyList<string> documentNames)
    {
        var body = new StringBuilder();
        body.AppendLine("LegalSynq");
        body.AppendLine("Awaiting Your Response");
        body.AppendLine();
        body.AppendLine("New Lien Offer");
        body.AppendLine("A medical lien has been submitted to your company for review and potential purchase. Review the asset overview below to proceed.");
        body.AppendLine();
        AppendTextSection(body, "Seller Information", sellerRows);
        AppendTextSection(body, "Asset Overview", assetRows);

        if (documentNames.Count > 0)
        {
            body.AppendLine("Supporting Documents");
            foreach (var documentName in documentNames)
                body.Append("- ").AppendLine(documentName);
            body.AppendLine();
        }

        body.Append("View Lien for Sale: ").AppendLine(buyerPortalUrl);
        body.AppendLine("This Link Expires in 30 Days");
        body.Append("This offer was sent on behalf of the ")
            .Append(sellerCompany)
            .Append(". Please reply directly to ")
            .Append(sellerEmail)
            .AppendLine(" for any questions.");

        return body.ToString();
    }

    private static void AppendTextSection(
        StringBuilder body,
        string title,
        IReadOnlyList<(string Label, string? Value)> rows)
    {
        body.AppendLine(title);
        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            body.Append(label).Append(": ").AppendLine(value.Trim());
        }
        body.AppendLine();
    }

    private async Task<string?> ResolveHandlingLawFirmAsync(
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (Guid.TryParse(metadata.GetValueOrDefault("lawFirmId"), out var lawFirmId))
        {
            var lawFirm = await _contactRepo.GetByIdAsync(tenantId, lawFirmId, ct);
            var name = FirstNonEmpty(lawFirm?.Organization, lawFirm?.DisplayName);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        var contacts = await _contactRepo.GetByOrgIdAsync(tenantId, caseEntity.OrgId, isActive: true, ct);
        var defaultLawFirm = contacts.FirstOrDefault(c =>
            string.Equals(c.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(c.ContactSubtype));

        return FirstNonEmpty(defaultLawFirm?.Organization, defaultLawFirm?.DisplayName);
    }

    private async Task<string?> ResolveCaseManagerAsync(
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (!Guid.TryParse(metadata.GetValueOrDefault("caseManagerId"), out var caseManagerId))
            return null;

        var caseManager = await _contactRepo.GetByIdAsync(tenantId, caseManagerId, ct);
        return FirstNonEmpty(caseManager?.DisplayName);
    }

    private async Task<List<string>> GetSupportingDocumentNamesAsync(
        Guid tenantId,
        Lien lien,
        Case? caseEntity,
        CancellationToken ct)
    {
        var names = new List<string>();
        var lienDocs = await _servicingItemRepo.SearchAsync(
            tenantId,
            search: null,
            status: null,
            priority: null,
            assignedTo: null,
            caseId: null,
            lienId: lien.Id,
            page: 1,
            pageSize: 100,
            ct: ct);
        names.AddRange(ExtractDocumentNames(lienDocs.Items));

        if (caseEntity is not null)
        {
            var caseDocs = await _servicingItemRepo.SearchAsync(
                tenantId,
                search: null,
                status: null,
                priority: null,
                assignedTo: null,
                caseId: caseEntity.Id,
                lienId: null,
                page: 1,
                pageSize: 100,
                ct: ct);
            names.AddRange(ExtractDocumentNames(caseDocs.Items));
        }

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ExtractDocumentNames(IEnumerable<ServicingItem> items)
    {
        foreach (var item in items.Where(IsDocumentServicingItem))
        {
            var fields = ParseLegacyNoteFields(item.Notes);
            var name = FirstNonEmpty(
                fields.GetValueOrDefault("originalFileName"),
                fields.GetValueOrDefault("filename"),
                item.Description);

            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }

    private static bool IsDocumentServicingItem(ServicingItem item)
        => string.Equals(item.TaskType, "LegacyCaseDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "LegacyLienDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "LegacyMedicalDocument", StringComparison.Ordinal);

    private static Contact? SelectSellerContact(IReadOnlyList<Contact> contacts)
        => contacts.FirstOrDefault(c =>
               string.Equals(c.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(c.ContactSubtype) &&
               !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault();

    private static ConfirmSellingLienSaleResponse MapConfirmSaleResponse(
        Lien lien,
        ConfirmSellingLienBuyerNotificationResponse? notification)
        => new()
        {
            LienId = lien.Id,
            LienCode = ResolveLienCode(lien),
            Status = lien.Status,
            SellerStatus = lien.SellerStatus ?? string.Empty,
            AskAmount = lien.AskAmount,
            OfferPrice = lien.OfferPrice,
            SubmittedForSaleAtUtc = lien.SubmittedForSaleAtUtc,
            SoldAtUtc = lien.SoldAtUtc,
            Notification = notification,
        };

    private static string BuildConfirmSaleNotificationIdempotencyKey(
        Guid tenantId,
        Guid lienId,
        Guid buyerContactId,
        string? requestIdempotencyKey)
    {
        var requestSegment = string.IsNullOrWhiteSpace(requestIdempotencyKey)
            ? "default"
            : requestIdempotencyKey.Trim();

        var key = string.Join(":", new[]
        {
            "liens.confirm-sale.email",
            tenantId.ToString("N"),
            lienId.ToString("N"),
            buyerContactId.ToString("N"),
            requestSegment,
        });

        return key.Length > 280 ? key[..280] : key;
    }

    private static bool IsSubmittedNotificationStatus(string? status)
        => string.Equals(status, "sent", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string ResolveLienCode(Lien lien)
        => string.IsNullOrWhiteSpace(lien.LienNumber) ? lien.Id.ToString() : lien.LienNumber;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string Html(string value)
        => WebUtility.HtmlEncode(value);

    private sealed record ConfirmSaleNotificationContext(
        Contact BuyerContact,
        Contact SellerContact,
        Case? Case,
        string HandlingLawFirm,
        string? CaseManager,
        IReadOnlyList<string> DocumentNames);

    private sealed record ConfirmSaleEmail(
        string Subject,
        string HtmlBody,
        string TextBody,
        Dictionary<string, string> TemplateData,
        IReadOnlyList<NotificationEmailInlineAttachment> InlineAttachments);

    private static void ValidateCreateRequest(CreateSellingPortfolioRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.PortfolioNumber))
            errors.Add("portfolioNumber", ["Portfolio number is required."]);
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("name", ["Name is required."]);

        if (request.LienIds.Any(id => id == Guid.Empty))
            errors.Add("lienIds", ["Lien ids cannot contain empty values."]);

        if (request.BuyerOrgIds.Any(id => id == Guid.Empty))
            errors.Add("buyerOrgIds", ["Buyer organization ids cannot contain empty values."]);

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);
    }

    private async Task<SellingPortfolio> RequirePortfolioAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        return await _portfolioRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Selling portfolio '{id}' not found for tenant '{tenantId}'.");
    }

    private async Task<Lien?> ResolveLienAsync(Guid tenantId, string lienIdOrCode, CancellationToken ct)
    {
        var value = lienIdOrCode.Trim();
        return Guid.TryParse(value, out var lienId)
            ? await _lienRepo.GetByIdAsync(tenantId, lienId, ct)
            : await _lienRepo.GetByLienNumberAsync(tenantId, value, ct);
    }

    private async Task<Lien?> ResolveLienForAssignmentAsync(Guid tenantId, string lienIdOrCode, CancellationToken ct)
    {
        var value = lienIdOrCode.Trim();
        return Guid.TryParse(value, out var lienId)
            ? await _lienRepo.GetByIdAnyTenantAsync(lienId, ct)
            : await _lienRepo.GetByLienNumberAsync(tenantId, value, ct);
    }

    private async Task<SellingPortfolioLien> CreateLienSnapshotAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid portfolioId,
        Guid lienId,
        Guid actingUserId,
        CancellationToken ct)
    {
        var lien = await _lienRepo.GetByIdAsync(tenantId, lienId, ct)
            ?? throw new ValidationException("Referenced lien does not exist.",
                new Dictionary<string, string[]> { ["lienIds"] = [$"Lien '{lienId}' not found."] });

        if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            throw new ValidationException("Referenced lien is not owned by the seller organization.",
                new Dictionary<string, string[]> { ["lienIds"] = [$"Lien '{lienId}' is not owned by seller organization '{sellerOrgId}'."] });

        string? caseExternalId = null;
        if (lien.CaseId.HasValue)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct);
            caseExternalId = caseEntity?.ExternalReference;
        }

        return SellingPortfolioLien.CreateSnapshot(tenantId, portfolioId, lien, caseExternalId, actingUserId);
    }

    private static void EnsureSellerPortfolio(SellingPortfolio portfolio, Guid sellerOrgId)
    {
        if (portfolio.SellerOrgId != sellerOrgId)
            throw new UnauthorizedAccessException("Selling portfolio does not belong to the current seller organization.");
    }

    private static List<string> BuildLienAssignmentRequests(AddSellingPortfolioLiensRequest request)
    {
        var result = new List<string>();
        result.AddRange(request.LienIds.Select(id => id.ToString()));
        result.AddRange(request.LienCodes);
        result.AddRange(request.Liens);
        return result;
    }

    private static Guid TryParseLienId(string lienIdOrCode) =>
        Guid.TryParse(lienIdOrCode, out var lienId) ? lienId : Guid.Empty;

    private static AddSellingPortfolioLienResult FailedLienResult(
        string requestedLien,
        Guid lienId,
        string? lienCode,
        string reasonCode,
        string message) => new()
        {
            RequestedLien = requestedLien,
            LienId = lienId,
            LienCode = lienCode,
            Success = false,
            Status = "rejected",
            ReasonCode = reasonCode,
            Message = message,
        };

    private void LogEligibilityFailure(
        Guid tenantId,
        Guid actingUserId,
        SellingPortfolio portfolio,
        Lien lien,
        LienEligibilityValidationResult eligibility)
    {
        var ruleCodes = string.Join(",", eligibility.Violations.Select(v => v.RuleCode));
        var messages = string.Join(" ", eligibility.Violations.Select(v => v.Message));

        _audit.Publish(
            eventType: "liens.selling_portfolio.lien_eligibility_failed",
            action: "LIEN_PORTFOLIO_ELIGIBILITY_VALIDATION_FAILED",
            description: $"Lien '{lien.LienNumber}' failed portfolio eligibility validation: {messages}",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: lien.Id.ToString(),
            metadata: $"{{\"portfolioId\":\"{portfolio.Id}\",\"lienId\":\"{lien.Id}\",\"ruleCodes\":\"{ruleCodes}\"}}");
    }

    private static RemoveSellingPortfolioLienResult FailedRemoveLienResult(
        Guid lienId,
        string? lienCode,
        string reasonCode,
        string message) => new()
        {
            LienId = lienId,
            LienCode = lienCode,
            Success = false,
            Status = "rejected",
            ReasonCode = reasonCode,
            Message = message,
        };

    private static string BuildPlaintiffName(Case? caseEntity, Lien lien)
    {
        var firstName = caseEntity?.ClientFirstName ?? lien.SubjectFirstName;
        var lastName = caseEntity?.ClientLastName ?? lien.SubjectLastName;
        var name = string.Join(" ", new[] { firstName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

        return string.IsNullOrWhiteSpace(name) ? "Unknown Plaintiff" : name;
    }

    private static SellingPortfolioResponse MapToResponse(SellingPortfolio entity)
    {
        return new SellingPortfolioResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SellerOrgId = entity.SellerOrgId,
            PortfolioNumber = entity.PortfolioNumber,
            Name = entity.Name,
            Description = entity.Description,
            InternalNotes = entity.InternalNotes,
            TargetGrouping = entity.TargetGrouping,
            Status = entity.Status,
            LienCount = entity.LienCount,
            OriginalAmountTotal = entity.OriginalAmountTotal,
            CurrentBalanceTotal = entity.CurrentBalanceTotal,
            OfferPriceTotal = entity.OfferPriceTotal,
            PublishedAtUtc = entity.PublishedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Liens = entity.Liens.Select(MapLien).ToList(),
            Buyers = entity.Buyers.Select(MapBuyer).ToList(),
        };
    }

    private static SellingPortfolioLienResponse MapLien(SellingPortfolioLien entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        LienId = entity.LienId,
        LienNumber = entity.LienNumber,
        LienExternalId = entity.LienExternalId,
        CaseId = entity.CaseId,
        CaseExternalId = entity.CaseExternalId,
        FacilityId = entity.FacilityId,
        LienType = entity.LienType,
        LienLifecycleStatus = entity.LienLifecycleStatus,
        OriginalAmount = entity.OriginalAmount,
        CurrentBalance = entity.CurrentBalance,
        OfferPrice = entity.OfferPrice,
        PurchasePrice = entity.PurchasePrice,
        PayoffAmount = entity.PayoffAmount,
        SubjectFirstName = entity.SubjectFirstName,
        SubjectLastName = entity.SubjectLastName,
        Jurisdiction = entity.Jurisdiction,
        IncidentDate = entity.IncidentDate,
        Description = entity.Description,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };

    private static SellingPortfolioBuyerResponse MapBuyer(SellingPortfolioBuyer entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        BuyerOrgId = entity.BuyerOrgId,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    private static SellingPortfolioStatusHistoryResponse MapStatusHistory(SellingPortfolioStatusHistory entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        FromStatus = entity.FromStatus,
        ToStatus = entity.ToStatus,
        ChangedByUserId = entity.ChangedByUserId,
        ChangedAtUtc = entity.ChangedAtUtc,
        Notes = entity.Notes,
    };

    private async Task InTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await operation();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task AddActivityAsync(
        SellingPortfolio portfolio,
        Guid actorUserId,
        string action,
        string entityType,
        string summary,
        string? entityId = null,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var activity = SellingPortfolioActivity.Create(
            portfolio.TenantId,
            portfolio.Id,
            action,
            entityType,
            actorUserId,
            summary,
            entityId,
            metadataJson);

        await _portfolioRepo.AddActivityAsync(activity, ct);
    }

    private static string ResolveStatusActivityAction(string status) => status switch
    {
        SellingPortfolioStatus.Published => "LIEN_SALE_PORTFOLIO_PUBLISHED",
        SellingPortfolioStatus.Withdrawn => "LIEN_SALE_PORTFOLIO_WITHDRAWN",
        _ => "LIEN_SALE_PORTFOLIO_STATUS_CHANGED",
    };

    private static List<SellingPortfolioAgingBucket> BuildAgingBuckets(IEnumerable<SellingPortfolioLien> liens)
    {
        var buckets = new Dictionary<string, (int Count, decimal Balance)>
        {
            ["0-30"] = (0, 0m),
            ["31-60"] = (0, 0m),
            ["61-90"] = (0, 0m),
            ["91-120"] = (0, 0m),
            ["120+"] = (0, 0m),
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var lien in liens)
        {
            var ageDays = lien.IncidentDate.HasValue
                ? Math.Max(0, today.DayNumber - lien.IncidentDate.Value.DayNumber)
                : Math.Max(0, (DateTime.UtcNow.Date - lien.CreatedAtUtc.Date).Days);
            var bucket = ageDays switch
            {
                <= 30 => "0-30",
                <= 60 => "31-60",
                <= 90 => "61-90",
                <= 120 => "91-120",
                _ => "120+",
            };
            var current = buckets[bucket];
            buckets[bucket] = (current.Count + 1, current.Balance + (lien.CurrentBalance ?? 0m));
        }

        return buckets.Select(kvp => new SellingPortfolioAgingBucket
        {
            Bucket = kvp.Key,
            LienCount = kvp.Value.Count,
            OutstandingBalance = kvp.Value.Balance,
        }).ToList();
    }

    private static List<SellingPortfolioConcentrationItem> BuildConcentrations(IEnumerable<SellingPortfolioLien> liens)
    {
        return liens
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Jurisdiction) ? "Unknown" : l.Jurisdiction)
            .Select(g => new SellingPortfolioConcentrationItem
            {
                Dimension = "Jurisdiction",
                Value = g.Key!,
                LienCount = g.Count(),
                OutstandingBalance = g.Sum(l => l.CurrentBalance ?? 0m),
            })
            .OrderByDescending(item => item.OutstandingBalance)
            .Take(10)
            .ToList();
    }

    private static SellingPortfolioActivityResponse MapActivity(SellingPortfolioActivity entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        Action = entity.Action,
        EntityType = entity.EntityType,
        EntityId = entity.EntityId,
        ActorUserId = entity.ActorUserId,
        OccurredAtUtc = entity.OccurredAtUtc,
        Summary = entity.Summary,
        MetadataJson = entity.MetadataJson,
    };
}
