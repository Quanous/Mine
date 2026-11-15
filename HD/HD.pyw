# HD - 바탕화면 디데이 위젯 (Windows / tkinter)
import json, os, sys, datetime as dt
import tkinter as tk
from tkinter import simpledialog, messagebox

APP = "HD"
BASE = getattr(sys, "_MEIPASS", os.path.dirname(os.path.abspath(__file__)))
DATA_DIR = os.path.join(os.environ.get("APPDATA", os.path.expanduser("~")), APP)
DATA = os.path.join(DATA_DIR, "data.json")

RED, BLACK, WHITE, KEY = "#e60012", "#0b0b0b", "#ffffff", "#010101"


def load():
    try:
        with open(DATA, encoding="utf-8") as f:
            d = json.load(f)
    except Exception:
        d = {}
    d.setdefault("events", [])
    d.setdefault("selected", 0)
    d.setdefault("x", 80)
    d.setdefault("y", 80)
    d.setdefault("top", False)
    return d


def save(d):
    os.makedirs(DATA_DIR, exist_ok=True)
    with open(DATA, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=1)


def dday(date_str):
    n = (dt.date.fromisoformat(date_str) - dt.date.today()).days
    if n == 0:
        return "D-Day", 0
    return (f"D-{n}" if n > 0 else f"D+{-n}"), n


def autostart_set(on):
    try:
        import winreg
        k = winreg.OpenKey(winreg.HKEY_CURRENT_USER,
                           r"Software\Microsoft\Windows\CurrentVersion\Run", 0, winreg.KEY_ALL_ACCESS)
        if on:
            exe = sys.executable if getattr(sys, "frozen", False) else \
                f'"{sys.executable.replace("python.exe", "pythonw.exe")}" "{os.path.abspath(__file__)}"'
            winreg.SetValueEx(k, APP, 0, winreg.REG_SZ, exe)
        else:
            try:
                winreg.DeleteValue(k, APP)
            except FileNotFoundError:
                pass
        return True
    except Exception:
        return False


def autostart_get():
    try:
        import winreg
        k = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Run")
        winreg.QueryValueEx(k, APP)
        return True
    except Exception:
        return False


class HD(tk.Tk):
    def __init__(self):
        super().__init__()
        self.d = load()
        self.title(APP)
        try:
            self.iconbitmap(os.path.join(BASE, "icon.ico"))
        except Exception:
            pass
        self.overrideredirect(True)
        self.configure(bg=KEY)
        try:
            self.attributes("-transparentcolor", KEY)
        except tk.TclError:
            pass
        self.attributes("-topmost", self.d["top"])
        self.geometry(f"+{self.d['x']}+{self.d['y']}")

        self.card = tk.Frame(self, bg=BLACK, highlightbackground=RED, highlightthickness=3)
        self.card.pack()
        try:
            self.img = tk.PhotoImage(file=os.path.join(BASE, "icon.png")).subsample(5, 5)
            self.pic = tk.Label(self.card, image=self.img, bg=BLACK, bd=0)
            self.pic.grid(row=0, column=0, rowspan=2, padx=(10, 4), pady=8)
        except Exception:
            self.pic = None
        self.name = tk.Label(self.card, bg=BLACK, fg=WHITE, font=("맑은 고딕", 12, "bold"))
        self.name.grid(row=0, column=1, padx=(4, 14), pady=(8, 0), sticky="w")
        self.big = tk.Label(self.card, bg=BLACK, fg=RED, font=("Impact", 34))
        self.big.grid(row=1, column=1, padx=(4, 14), pady=(0, 4), sticky="w")
        self.sub = tk.Label(self.card, bg=BLACK, fg="#aaaaaa", font=("맑은 고딕", 9))
        self.sub.grid(row=2, column=0, columnspan=2, pady=(0, 6))

        for w in (self, self.card, self.name, self.big, self.sub, *( [self.pic] if self.pic else [] )):
            w.bind("<ButtonPress-1>", self.start_drag)
            w.bind("<B1-Motion>", self.drag)
            w.bind("<ButtonRelease-1>", self.end_drag)
            w.bind("<Button-3>", self.menu)
            w.bind("<Double-Button-1>", self.next_event)
        self.refresh()
        self.tick()

    # --- 표시 ---
    def cur(self):
        ev = self.d["events"]
        if not ev:
            return None
        self.d["selected"] %= len(ev)
        return ev[self.d["selected"]]

    def refresh(self):
        e = self.cur()
        if not e:
            self.name.config(text="우클릭 → 디데이 추가")
            self.big.config(text="HD")
            self.sub.config(text="")
            return
        txt, n = dday(e["date"])
        self.name.config(text=e["name"])
        self.big.config(text=txt, fg=RED if n >= 0 else "#888888")
        self.sub.config(text=e["date"])

    def tick(self):
        self.refresh()
        self.after(60_000, self.tick)

    # --- 이동 ---
    def start_drag(self, e):
        self._dx, self._dy = e.x_root - self.winfo_x(), e.y_root - self.winfo_y()

    def drag(self, e):
        self.geometry(f"+{e.x_root - self._dx}+{e.y_root - self._dy}")

    def end_drag(self, _):
        self.d["x"], self.d["y"] = self.winfo_x(), self.winfo_y()
        save(self.d)

    # --- 동작 ---
    def next_event(self, _=None):
        if self.d["events"]:
            self.d["selected"] = (self.d["selected"] + 1) % len(self.d["events"])
            save(self.d)
            self.refresh()

    def ask_date(self, init=""):
        s = simpledialog.askstring(APP, "날짜 (YYYY-MM-DD)", initialvalue=init, parent=self)
        if s is None:
            return None
        try:
            return dt.date.fromisoformat(s.strip()).isoformat()
        except ValueError:
            messagebox.showerror(APP, "날짜 형식이 틀렸어요. 예: 2026-12-25", parent=self)
            return None

    def add(self):
        name = simpledialog.askstring(APP, "디데이 이름", parent=self)
        if not name:
            return
        date = self.ask_date()
        if date:
            self.d["events"].append({"name": name, "date": date})
            self.d["selected"] = len(self.d["events"]) - 1
            save(self.d)
            self.refresh()

    def edit(self):
        e = self.cur()
        if not e:
            return
        name = simpledialog.askstring(APP, "디데이 이름", initialvalue=e["name"], parent=self)
        if not name:
            return
        date = self.ask_date(e["date"])
        if date:
            e["name"], e["date"] = name, date
            save(self.d)
            self.refresh()

    def delete(self):
        e = self.cur()
        if e and messagebox.askyesno(APP, f"'{e['name']}' 삭제할까요?", parent=self):
            self.d["events"].remove(e)
            save(self.d)
            self.refresh()

    def toggle_top(self):
        self.d["top"] = not self.d["top"]
        self.attributes("-topmost", self.d["top"])
        save(self.d)

    def toggle_auto(self):
        if not autostart_set(not autostart_get()):
            messagebox.showinfo(APP, "자동 실행 설정은 Windows에서만 가능해요.", parent=self)

    def menu(self, e):
        m = tk.Menu(self, tearoff=0)
        m.add_command(label="디데이 추가", command=self.add)
        m.add_command(label="수정", command=self.edit)
        m.add_command(label="삭제", command=self.delete)
        if len(self.d["events"]) > 1:
            m.add_separator()
            for i, ev in enumerate(self.d["events"]):
                m.add_command(label=f"{ev['name']} ({dday(ev['date'])[0]})",
                              command=lambda i=i: (self.d.update(selected=i), save(self.d), self.refresh()))
        m.add_separator()
        m.add_checkbutton(label="항상 위에 표시", command=self.toggle_top,
                          variable=tk.BooleanVar(value=self.d["top"]))
        m.add_checkbutton(label="윈도우 시작 시 자동 실행", command=self.toggle_auto,
                          variable=tk.BooleanVar(value=autostart_get()))
        m.add_separator()
        m.add_command(label="종료", command=self.destroy)
        m.tk_popup(e.x_root, e.y_root)


if __name__ == "__main__":
    HD().mainloop()
