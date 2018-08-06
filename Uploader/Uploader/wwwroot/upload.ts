

class uploader {


  private host = "/";


  public constructor(file: File) {
    this.file = file;
  }


  private file: File;
  private token: string;
  private running: boolean = false;



  public getToken(): string {
    return this.token;
  }


  public start(token: string = null) {

    if (this.running || this.completed)
      return;

    if (token != null && token != "")
      this.token = token;



    if (this.token == null) {

      var request = this.createRequest();
      request.onloadend = () => {
        this.token = JSON.parse(request.responseText).token;
        this.start();
      };

      request.open("GET", this.host + "?filesize=" + this.file.size);
      request.send();
    }
    else {
      this.running = true;
      this.uploadCore();
    }
  }


  public stop() {
    this.running = false;
  }



  private createRequest(): XMLHttpRequest {
    var instance = new XMLHttpRequest();
    instance.onerror = data => this.error(data);
    instance.ontimeout = data => this.error(data);
    instance.onloadend = () => this.uploadCallback(JSON.parse(instance.responseText));

    return instance;
  }


  private error(data: any): void {

    console.log(data);

    this.running = false;
    if (this.onerror != null)
      this.onerror(data);
  }


  private uploadCore() {

    var request = this.createRequest();

    request.open("GET", this.host + this.token);
    request.send();

  }


  private uploadCallback(data: any) {

    if (this.completed)
      return;

    if (this.onprogress != null)
      this.onprogress(data);


    if (this.running == false)
      return;


    var incomplete = <incompleteMeta>data.incomplete;

    if (incomplete == null) {

      if (data.IncompleteBlocks !== 0) {
        this.error(data);
        return;
      }
      else {
        this.complete();
        return;
      }
    }

    this.uploadBlock(incomplete);
  }


  private uploadBlock(incomplete: incompleteMeta) {


    var block = this.file.slice(incomplete.start, incomplete.end, "application/octet-stream");


    var request = this.createRequest();

    request.open("POST", this.host + this.token + "/" + incomplete.blockIndex);
    request.send(block);

  }


  private complete() {

    var request = this.createRequest();
    request.onloadend = () => {
      this.completeData = JSON.parse(request.responseText);
      this.completed = true;

      if (this.oncomplete != null)
        this.oncomplete(this.completeData);

    }

    request.open("GET", this.host + this.token + "/?filename=" + this.file.name);
    request.send();
  }


  private completed: boolean = false;
  private completeData: any;

  public getCompleteData(): any {
    if (this.completed == false)
      return null;

    return this.completeData;
  }


  public oncomplete: (data: any) => void;

  public onprogress: (data: any) => void;

  public onerror: (data: any) => void;


  public get status(): string {
    if (this.completed)
      return "completed";

    else if (this.running)
      return "running";

    else
      return "stopped";

  }
}

interface incompleteMeta {
  blockIndex: number;

  start: number;
  end: number;

}
