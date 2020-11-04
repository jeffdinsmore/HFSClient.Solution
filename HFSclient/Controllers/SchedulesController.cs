using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using HFSclient.Models;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using HFSclient.Wrappers;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HFSclient.Controllers
{
  public class SchedulesController : Controller
  {
    public readonly HFSclientContext _db;
    public SchedulesController(HFSclientContext db)
    {
      _db = db;
    }
    public  ActionResult  Index()
    {
        
      List<Schedule> model = _db.Schedules.ToList();
      return View(model);
    }

    [Authorize(Roles = "Administrator")]
    public ActionResult Create()
    {
      return View();
    }
  
    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public ActionResult Create(int GroupId1, int GroupId2, int Week, int Team1Score, int Team2Score )
    {
      Schedule schedule = new Schedule();
      if(GroupId1 != 0)
      {
        schedule.GroupId1 = GroupId1;
      }
      if(GroupId2 != 0)
      {
        schedule.GroupId2 = GroupId2;
      }
      if(Week != 0)
      {
        schedule.Week = Week;
      }
      if(Team1Score != 0)
      {
        schedule.Team1Score = Team1Score;
      }
      if(Team2Score != 0)
      {
        schedule.Team2Score = Team2Score;
      }

      _db.Schedules.Add(schedule);
      _db.SaveChanges();
      return RedirectToAction("Index");
    }
    public ActionResult Details(int id)
    {
      var thisSchedule = _db.Schedules
        .FirstOrDefault(x => x.ScheduleId == id);
      ViewBag.Team1Trackers = _db.Trackers.Where(x => x.ScheduleId == id && x.GroupId == thisSchedule.GroupId1).Include(x => x.Player).Include(x =>x.Game).OrderBy(x => x.Position); 
      ViewBag.Team2Trackers = _db.Trackers.Where(x => x.ScheduleId == id && x.GroupId == thisSchedule.GroupId2).Include(x => x.Player).Include(x =>x.Game).OrderBy(x => x.Position); 
      ViewBag.Team1Name = _db.Groups.Where(c => c.GroupId ==  thisSchedule.GroupId1);
      ViewBag.Team2Name = _db.Groups.Where(c => c.GroupId ==  thisSchedule.GroupId2);
      return View(thisSchedule);
    }

    [Authorize(Roles = "Administrator")]
    public ActionResult Edit(int id)
    {
      var thisSchedule = _db.Schedules
        .FirstOrDefault(x => x.ScheduleId == id);
      return View(thisSchedule);
    }
    
    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public ActionResult Edit(Schedule schedule)
    {
      _db.Entry(schedule).State = EntityState.Modified;
      _db.SaveChanges();
      return RedirectToAction("Details", new { id = schedule.ScheduleId});
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public async Task<ActionResult> Sim(Schedule schedule ) //might be schedule id
    {
      // Schedule schedule = new Schedule(); //likely be changed
      // schedule.ScheduleId = _db.Schedules.Count() + 1; //likely be changed
      // if(GroupId1 != 0)
      // {
      //   schedule.GroupId1 = GroupId1;
      // }
      // if(GroupId2 != 0)
      // {
      //   schedule.GroupId2 = GroupId2;
      // }
      // if(Week != 0)
      // {
      //   schedule.Week = Week;
      // }
      List<Roster> roster1 = _db.Rosters.Where(x => x.GroupId == schedule.GroupId1).ToList();
      List<Roster> roster2 = _db.Rosters.Where(x => x.GroupId == schedule.GroupId2).ToList();
      int teamscore = 0;
      foreach (Roster roster in roster1)
      {
          Game game = await Game.GetGameByWeek(schedule.Week, roster.PlayerId); //get game by roster.GameId;
          Tracker tracker = new Tracker();
          tracker.GameId = game.GameId;
          tracker.GroupId = schedule.GroupId1;
          tracker.Position = roster.Position;
          tracker.Points = game.CalcScore(); //calculate fantasy points here 
          if(tracker.Position != "bench")//make sure not a bench player and add to schedule points for the team
          {
            teamscore += tracker.Points;
          }
          tracker.ScheduleId = schedule.ScheduleId;
          _db.Trackers.Add(tracker);
      }
      schedule.Team1Score = teamscore;
      teamscore = 0;
      foreach (Roster roster in roster2)
      {
          Game game = await Game.GetGameByWeek(schedule.Week, roster.PlayerId); //get game by roster.GameId;
          Tracker tracker = new Tracker();
          tracker.GameId = game.GameId;
          tracker.GroupId = schedule.GroupId2;
          tracker.Position = roster.Position;
          tracker.Points = game.CalcScore(); //calculate fantasy points here 
          if(tracker.Position != "bench")//make sure not a bench player and add to schedule points for the team
          {
            teamscore += tracker.Points;
          }
          tracker.ScheduleId = schedule.ScheduleId;
          _db.Trackers.Add(tracker);
          
      }
      schedule.Team2Score = teamscore;
      //update standings

      Standing standingTeam1 = _db.Standings.Where(x => x.GroupId == schedule.GroupId1).FirstOrDefault();
      Standing standingTeam2 = _db.Standings.Where(x => x.GroupId == schedule.GroupId2).FirstOrDefault();

      if(schedule.Team1Score > schedule.Team2Score)
      {
        standingTeam1.Wins++;
        standingTeam2.Losses++;
        standingTeam1.PtsFor += schedule.Team1Score;
        standingTeam2.PtsFor += schedule.Team2Score;
        standingTeam1.PtsAgst += schedule.Team2Score;
        standingTeam1.PtsAgst += schedule.Team1Score;
        
      }
      else if(schedule.Team1Score < schedule.Team2Score)
      {
        standingTeam2.Wins++;
        standingTeam1.Losses++;
        standingTeam2.PtsFor += schedule.Team2Score;
        standingTeam1.PtsFor += schedule.Team1Score;
        standingTeam2.PtsAgst += schedule.Team1Score;
        standingTeam1.PtsAgst += schedule.Team2Score;
        
      }
      else
      {
        standingTeam2.Ties++;
        standingTeam1.Ties++;
        standingTeam2.PtsFor += schedule.Team2Score;
        standingTeam1.PtsFor += schedule.Team1Score;
        standingTeam2.PtsAgst += schedule.Team1Score;
        standingTeam1.PtsAgst += schedule.Team2Score;
      }





      _db.Entry(standingTeam1).State = EntityState.Modified; 
      _db.Entry(standingTeam2).State = EntityState.Modified;


      _db.Schedules.Add(schedule);
      _db.SaveChanges();
      return RedirectToAction("Index"); //enter proper view / next sim
    }

    [Authorize(Roles = "Administrator")]// 12345678  1   2   3  4  5 6   7  8
                                        // 1         12  13 14 15 16 17 18  12
                                        //2     12  24 25 26 27 28 23  12
                                        // 3    34  31 
    public ActionResult BuildSchedule(int LeagueId)                                         
    {
      List<Group> temp = _db.Groups.Where(x => x.LeagueId == LeagueId).ToList();
      List<int> teams = new List<int>();
      foreach (Group group in temp)
      {
          teams.Append(group.GroupId);
      }
      //let teams = ["1",'2','3','4','5','6','7','8','9']
      int count = teams.Count();
      if( count % 2 == 1)
      {
        count++;
        teams.Append(0);
      }
      
      List<int> teams1 = teams.GetRange(0,count/2);
      // let teams1 = ["1",'2','3','4','5'];//5742
      // let teams2 = ['6','7','8','9',''];

      List<int> teams2 =teams.GetRange(count/2, count/2);

      
      // if(count % 2 == 1)
      // {
      // count++;
      // teams2.push('');
      // }
      for(int i = 0 ; i < 12; i++)
      {
          // console.log(teams1)
          // console.log(teams2)
          // console.log('')
        for(int j = 0; j < count / 2; j++)
        {
          //console.log(teams1[j] + teams2[j]);//create game
          Schedule schedule =new Schedule();
          schedule.GroupId1 = teams1[j];
          schedule.GroupId2 = teams2[j];
          schedule.IsComplete = false;
          schedule.Week = i + 1;
          _db.Schedules.Add(schedule);
          
        }
        
        if(i % (count/2 - 1) == 0 && i != 0)
        {
          int tmp = teams1[count/2 - 1];
          teams1.RemoveAt(count/2 - 1);

          teams2.Add(tmp);
         
          tmp = teams2[0];
          teams2.RemoveAt(0);
          int temp2 = teams1[0];
          teams1.RemoveAt(0);
          
          teams1.Prepend(tmp);
          teams1.Prepend(temp2);
       
        }
        else
        {
          int tmp = teams1[count/2 - 1];
          teams1.RemoveAt(count/2 - 1);
          //let temp = teams1.pop();
          
          teams2.Add(tmp);
          
          tmp = teams2[0];
          teams2.RemoveAt(0);
                    
          teams1.Prepend(tmp);
        
          
          // temp = teams2.shift();
          // teams1.unshift(temp)
          
        }
      }
      _db.SaveChanges();
      return RedirectToAction("index");// need to change to proper view
    }
    public ActionResult LeagueSchedule(int id) //League Id
    {
      var teams = _db.Groups.Where(x => x.LeagueId  == id).ToList();
      List<Schedule> games = new List<Schedule>();
      foreach (Group team in teams)
      {
        List<Schedule> s = _db.Schedules().Where(x => x.GroupId1 == team.GroupId);
        foreach (Schedule  schedule in s)
        {
            games.Add(schedule);
        }
          
      }
        
      
      return View(games.OrderBy(x => x.Week));
    }

    
  }
}

