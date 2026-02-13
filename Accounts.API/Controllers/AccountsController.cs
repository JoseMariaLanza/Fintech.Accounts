using Accounts.Application.Accounts.Commands;
using Accounts.Application.Accounts.DTOs;
using Accounts.Application.Accounts.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Accounts.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AccountsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AccountDto>> Get(Guid id)
        {
            var dto = await _mediator.Send(new GetAccountByIdQuery(id));
            return dto is null ? NotFound(NotFound($"Account {id} not found.")) : Ok(dto);
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferFundsCommand command)
        {
            await _mediator.Send(command); // devuelve Unit, pero no lo usamos
            return NoContent();
        }
    }
}
