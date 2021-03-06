#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyBallot.Data;
using TinyBallot.Models;

namespace tinyballot.Controllers
{
    public class SimplePollController : Controller
    {
        private readonly SimplePollContext _context;

        public SimplePollController(SimplePollContext context)
        {
            _context = context;
        }

        // GET: SimplePoll
        public async Task<IActionResult> Index()
        {
            var polls = await _context.Polls
                .Include(p => p.Candidates)
                .Include(p => p.Ballots)
                .Select(p => new PollBriefDTO(p))
                .ToListAsync();
            
            return View(polls);
        }

        // GET: SimplePoll/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poll = await _context.Polls
                .Include(p => p.Candidates)
                .Include(p => p.Ballots)
                .ThenInclude(b => b.BallotCandidates)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.PollId == id);
            
            if (poll == null)
            {
                return NotFound();
            }

            return View(new PollDTO(poll));
        }

        // GET: SimplePoll/Create
        public IActionResult Create()
        {
            return View(new PollHeaderDTO());
        }

        // POST: SimplePoll/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PollHeaderDTO pollDTO)
        {
            if (ModelState.IsValid)
            {
                Poll poll = new Poll
                {
                    PollId = pollDTO.PollId,
                    Name = pollDTO.Name,
                    Description = pollDTO.Description,
                    Candidates = (from c in pollDTO.Candidates
                                  select new Candidate()
                                  {
                                      CandidateId = c.CandidateId,
                                      Label = c.Label
                                  }).ToList()
                };
                _context.Add(poll);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(pollDTO);
        }

        // GET: SimplePoll/Vote/5
        public async Task<IActionResult> Vote(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poll = await _context.Polls
                .Include(p => p.Candidates)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.PollId == id);

            if (poll == null)
            {
                return NotFound();
            }

            var header = new PollHeaderDTO(poll);
            var ballot = new BallotDTO() { PollId = poll.PollId };

            return View(new Tuple<PollHeaderDTO, BallotDTO>(header, ballot));
        }

        // POST: SimplePoll/Vote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(int? id, BallotDTO ballotDTO)
        {
            if (id != ballotDTO.PollId)
            {
                return NotFound();
            }

            var poll = await _context.Polls
                .Include(p => p.Ballots)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.PollId == id);

            if (poll == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var ballot = new Ballot()
                    {
                        BallotId = ballotDTO.BallotId,
                        PollId = ballotDTO.PollId,
                        Voter = ballotDTO.Voter,
                        BallotCandidates = (from c in ballotDTO.Candidates
                            select new BallotCandidate()
                                {
                                    BallotId = ballotDTO.BallotId,
                                    CandidateId = c
                                }
                        ).ToList()
                    };

                    poll.Ballots.Add(ballot);
                    _context.Update(poll);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PollExists(poll.PollId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            
            poll = await _context.Polls
                .Include(p => p.Candidates)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.PollId == id);
            
            return View(new Tuple<PollHeaderDTO, BallotDTO>(new PollHeaderDTO(poll), ballotDTO));
        }

        public IActionResult AddCandidate()
        {
            return PartialView("CandidateRow", new CandidateDTO());
        }
        
        // GET: SimplePoll/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poll = await _context.Polls
                .Include(p => p.Candidates)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.PollId == id);
            
            if (poll == null)
            {
                return NotFound();
            }
            
            return View(new PollHeaderDTO(poll));
        }

        // POST: SimplePoll/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PollHeaderDTO pollDTO)
        {
            if (id != pollDTO.PollId)
            {
                return NotFound();
            }

            var poll = await _context.Polls
                .Include(p => p.Candidates)
                .ThenInclude(c => c.BallotCandidates)
                .Include(p => p.Ballots)
                .ThenInclude(b => b.BallotCandidates)
                .FirstOrDefaultAsync(p => p.PollId == id);

            if (poll == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    poll.Name = pollDTO.Name;
                    poll.Description = pollDTO.Description;

                    // keep given candidates that were there already
                    var newCandidates = (from c in poll.Candidates
                                         where pollDTO.Candidates.Any(cDTO => cDTO.CandidateId == c.CandidateId)
                                         select c
                    ).ToList();
                    foreach (var cDTO in pollDTO.Candidates)
                    {
                        var tmpC = newCandidates.FirstOrDefault(c => c.CandidateId == cDTO.CandidateId);
                        if (tmpC == null)
                        {
                            // If new candidate, just add them to list
                            newCandidates.Add(new Candidate(){ Label = cDTO.Label });
                        }
                        else
                        {
                            // If candidate was already there, update it
                            tmpC.Label = cDTO.Label;
                        }
                    }
                    // update poll with new candidate list
                    poll.Candidates = newCandidates;

                    _context.Update(poll);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PollExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(pollDTO);
        }

        // GET: SimplePoll/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            
            var poll = await _context.Polls
                .Include(p => p.Candidates)
                .Include(p => p.Ballots)
                .AsSingleQuery()
                .FirstOrDefaultAsync(m => m.PollId == id);
            
            if (poll == null)
            {
                return NotFound();
            }
            
            return View(poll);
        }

        // POST: SimplePoll/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var poll = await _context.Polls
                .Include(p => p.Candidates)
                .Include(p => p.Ballots)
                .ThenInclude(b => b.BallotCandidates)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.PollId == id);
            _context.Polls.Remove(poll);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PollExists(int id)
        {
            return _context.Polls.Any(e => e.PollId == id);
        }
    }
}
