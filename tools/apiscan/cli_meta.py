#!/usr/bin/env python3
"""Minimal ECMA-335 CLI metadata reader (no dotnet/mono needed)."""
import struct, sys, re, os, signal
try:
    signal.signal(signal.SIGPIPE, signal.SIG_DFL)
except Exception:
    pass

def u1(b,o): return b[o]
def u2(b,o): return struct.unpack_from('<H',b,o)[0]
def u4(b,o): return struct.unpack_from('<I',b,o)[0]

CODED = {
 'TypeDefOrRef':([0x02,0x01,0x1B],2),
 'HasConstant':([0x04,0x08,0x17],2),
 'HasCustomAttribute':([0x06,0x04,0x01,0x02,0x08,0x09,0x0A,0x00,0x0E,0x17,0x14,0x11,0x1A,0x1B,0x20,0x23,0x26,0x27,0x28,0x2A,0x2C,0x2B],5),
 'HasFieldMarshal':([0x04,0x08],1),
 'HasDeclSecurity':([0x02,0x06,0x20],2),
 'MemberRefParent':([0x02,0x01,0x1A,0x06,0x1B],3),
 'HasSemantics':([0x14,0x17],1),
 'MethodDefOrRef':([0x06,0x0A],1),
 'MemberForwarded':([0x04,0x06],1),
 'Implementation':([0x26,0x23,0x27],2),
 'CustomAttributeType':([-1,-1,0x06,0x0A,-1],3),
 'ResolutionScope':([0x00,0x1A,0x23,0x01],2),
 'TypeOrMethodDef':([0x02,0x06],1),
}

S=('str',); G=('guid',); B=('blob',)
def R(t): return ('rid',t)
def C(n): return ('cod',n)

TABLES={
0x00:('Module',[('Generation','u2'),('Name',S),('Mvid',G),('EncId',G),('EncBaseId',G)]),
0x01:('TypeRef',[('ResolutionScope',C('ResolutionScope')),('Name',S),('Namespace',S)]),
0x02:('TypeDef',[('Flags','u4'),('Name',S),('Namespace',S),('Extends',C('TypeDefOrRef')),('FieldList',R(0x04)),('MethodList',R(0x06))]),
0x03:('FieldPtr',[('Field',R(0x04))]),
0x04:('Field',[('Flags','u2'),('Name',S),('Signature',B)]),
0x05:('MethodPtr',[('Method',R(0x06))]),
0x06:('MethodDef',[('RVA','u4'),('ImplFlags','u2'),('Flags','u2'),('Name',S),('Signature',B),('ParamList',R(0x08))]),
0x07:('ParamPtr',[('Param',R(0x08))]),
0x08:('Param',[('Flags','u2'),('Sequence','u2'),('Name',S)]),
0x09:('InterfaceImpl',[('Class',R(0x02)),('Interface',C('TypeDefOrRef'))]),
0x0A:('MemberRef',[('Class',C('MemberRefParent')),('Name',S),('Signature',B)]),
0x0B:('Constant',[('Type','u1'),('Pad','u1'),('Parent',C('HasConstant')),('Value',B)]),
0x0C:('CustomAttribute',[('Parent',C('HasCustomAttribute')),('Type',C('CustomAttributeType')),('Value',B)]),
0x0D:('FieldMarshal',[('Parent',C('HasFieldMarshal')),('NativeType',B)]),
0x0E:('DeclSecurity',[('Action','u2'),('Parent',C('HasDeclSecurity')),('PermissionSet',B)]),
0x0F:('ClassLayout',[('PackingSize','u2'),('ClassSize','u4'),('Parent',R(0x02))]),
0x10:('FieldLayout',[('Offset','u4'),('Field',R(0x04))]),
0x11:('StandAloneSig',[('Signature',B)]),
0x12:('EventMap',[('Parent',R(0x02)),('EventList',R(0x14))]),
0x13:('EventPtr',[('Event',R(0x14))]),
0x14:('Event',[('EventFlags','u2'),('Name',S),('EventType',C('TypeDefOrRef'))]),
0x15:('PropertyMap',[('Parent',R(0x02)),('PropertyList',R(0x17))]),
0x16:('PropertyPtr',[('Property',R(0x17))]),
0x17:('Property',[('Flags','u2'),('Name',S),('Type',B)]),
0x18:('MethodSemantics',[('Semantics','u2'),('Method',R(0x06)),('Association',C('HasSemantics'))]),
0x19:('MethodImpl',[('Class',R(0x02)),('MethodBody',C('MethodDefOrRef')),('MethodDeclaration',C('MethodDefOrRef'))]),
0x1A:('ModuleRef',[('Name',S)]),
0x1B:('TypeSpec',[('Signature',B)]),
0x1C:('ImplMap',[('MappingFlags','u2'),('MemberForwarded',C('MemberForwarded')),('ImportName',S),('ImportScope',R(0x1A))]),
0x1D:('FieldRVA',[('RVA','u4'),('Field',R(0x04))]),
0x1E:('EncLog',[('Token','u4'),('FuncCode','u4')]),
0x1F:('EncMap',[('Token','u4')]),
0x20:('Assembly',[('HashAlgId','u4'),('Major','u2'),('Minor','u2'),('Build','u2'),('Rev','u2'),('Flags','u4'),('PublicKey',B),('Name',S),('Culture',S)]),
0x21:('AssemblyProcessor',[('Processor','u4')]),
0x22:('AssemblyOS',[('OSPlatformID','u4'),('OSMajor','u4'),('OSMinor','u4')]),
0x23:('AssemblyRef',[('Major','u2'),('Minor','u2'),('Build','u2'),('Rev','u2'),('Flags','u4'),('PublicKeyOrToken',B),('Name',S),('Culture',S),('HashValue',B)]),
0x24:('AssemblyRefProcessor',[('Processor','u4'),('AssemblyRef',R(0x23))]),
0x25:('AssemblyRefOS',[('OSPlatformID','u4'),('OSMajor','u4'),('OSMinor','u4'),('AssemblyRef',R(0x23))]),
0x26:('File',[('Flags','u4'),('Name',S),('HashValue',B)]),
0x27:('ExportedType',[('Flags','u4'),('TypeDefId','u4'),('Name',S),('Namespace',S),('Implementation',C('Implementation'))]),
0x28:('ManifestResource',[('Offset','u4'),('Flags','u4'),('Name',S),('Implementation',C('Implementation'))]),
0x29:('NestedClass',[('NestedClass',R(0x02)),('EnclosingClass',R(0x02))]),
0x2A:('GenericParam',[('Number','u2'),('Flags','u2'),('Owner',C('TypeOrMethodDef')),('Name',S)]),
0x2B:('MethodSpec',[('Method',C('MethodDefOrRef')),('Instantiation',B)]),
0x2C:('GenericParamConstraint',[('Owner',R(0x2A)),('Constraint',C('TypeDefOrRef'))]),
}

ET={0x01:'void',0x02:'bool',0x03:'char',0x04:'sbyte',0x05:'byte',0x06:'short',0x07:'ushort',0x08:'int',
0x09:'uint',0x0a:'long',0x0b:'ulong',0x0c:'float',0x0d:'double',0x0e:'string',0x16:'TypedReference',
0x18:'IntPtr',0x19:'UIntPtr',0x1c:'object'}

class Asm:
    def __init__(self, path):
        self.path=path
        self.b=open(path,'rb').read()
        self._pe(); self._meta(); self._tables(); self._index()

    def _pe(self):
        b=self.b
        pe=u4(b,0x3C)
        assert b[pe:pe+4]==b'PE\0\0', 'not PE'
        coff=pe+4
        nsec=u2(b,coff+2); optsz=u2(b,coff+16)
        opt=coff+20
        magic=u2(b,opt)
        ddoff = opt+(96 if magic==0x10b else 112)
        self.sections=[]
        s=opt+optsz
        for i in range(nsec):
            o=s+i*40
            self.sections.append((u4(b,o+12),u4(b,o+8),u4(b,o+20),u4(b,o+16)))  # va, vsize, raw, rawsize
        cli_rva=u4(b,ddoff+14*8)
        cli=self.rva(cli_rva)
        self.md_rva=u4(b,cli+8); self.md_size=u4(b,cli+12)
        self.cli_flags=u4(b,cli+16)

    def rva(self,r):
        for va,vs,raw,rs in self.sections:
            if va<=r<va+max(vs,rs): return raw+(r-va)
        raise ValueError('bad rva %x'%r)

    def _meta(self):
        b=self.b; m=self.rva(self.md_rva)
        assert u4(b,m)==0x424A5342,'no BSJB'
        vlen=u4(b,m+12)
        self.runtime_version=b[m+16:m+16+vlen].split(b'\0')[0].decode()
        o=m+16+vlen
        nstr=u2(b,o+2); o+=4
        self.streams={}
        for _ in range(nstr):
            so=u4(b,o); ss=u4(b,o+4); o+=8
            e=b.index(b'\0',o); name=b[o:e].decode()
            o+=((e-o)//4+1)*4
            self.streams[name]=(m+so,ss)
        self.strings=self.streams.get('#Strings'); self.blobs=self.streams.get('#Blob')
        self.guids=self.streams.get('#GUID'); self.usr=self.streams.get('#US')

    def s(self,i):
        if not self.strings or i==0: return ''
        o=self.strings[0]+i; e=self.b.index(b'\0',o)
        return self.b[o:e].decode('utf-8','replace')

    def blob(self,i):
        if not self.blobs: return b''
        o=self.blobs[0]+i
        v,n=self._cint(self.b,o)
        return self.b[o+n:o+n+v]

    @staticmethod
    def _cint(b,o):
        x=b[o]
        if x&0x80==0: return x,1
        if x&0x40==0: return ((x&0x3f)<<8)|b[o+1],2
        return ((x&0x1f)<<24)|(b[o+1]<<16)|(b[o+2]<<8)|b[o+3],4

    def _tables(self):
        b=self.b; o=self.streams['#~'][0]
        heap=u1(b,o+6)
        self.sstr=4 if heap&1 else 2
        self.sguid=4 if heap&2 else 2
        self.sblob=4 if heap&4 else 2
        valid=struct.unpack_from('<Q',b,o+8)[0]
        o+=24
        self.rows={}
        present=[i for i in range(64) if valid>>i & 1]
        for t in present:
            self.rows[t]=u4(b,o); o+=4
        def sz(c):
            if c=='u1': return 1
            if c=='u2': return 2
            if c in('u4',): return 4
            if c==S: return self.sstr
            if c==G: return self.sguid
            if c==B: return self.sblob
            if c[0]=='rid': return 2 if self.rows.get(c[1],0)<0x10000 else 4
            tl,bits=CODED[c[1]]
            mx=max([self.rows.get(t,0) for t in tl if t>=0]+[0])
            return 2 if mx < (1<<(16-bits)) else 4
        self.tbl={}
        for t in present:
            if t not in TABLES:
                raise ValueError('unknown table 0x%02x'%t)
            name,cols=TABLES[t]
            widths=[sz(c) for _,c in cols]
            rw=sum(widths)
            offs=[]; a=0
            for w in widths: offs.append(a); a+=w
            self.tbl[t]=(o,rw,cols,widths,offs)
            o+=rw*self.rows[t]

    def row(self,t,rid):
        """1-based rid -> dict"""
        if t not in self.tbl or rid<1 or rid>self.rows[t]: return None
        base,rw,cols,widths,offs=self.tbl[t]
        o=base+(rid-1)*rw; b=self.b; d={}
        for (nm,c),w,off in zip(cols,widths,offs):
            raw = b[o+off] if w==1 else (u2(b,o+off) if w==2 else u4(b,o+off))
            if c==S: d[nm]=self.s(raw)
            elif c==B: d[nm]=raw
            elif c==G: d[nm]=raw
            elif isinstance(c,tuple) and c[0]=='rid': d[nm]=raw
            elif isinstance(c,tuple) and c[0]=='cod':
                tl,bits=CODED[c[1]]
                tag=raw&((1<<bits)-1); r=raw>>bits
                tt=tl[tag] if tag<len(tl) else -1
                d[nm]=(tt,r)
            else: d[nm]=raw
        return d

    def nrows(self,t): return self.rows.get(t,0)
    def rows_of(self,t):
        for i in range(1,self.nrows(t)+1): yield i,self.row(t,i)

    def _index(self):
        self.nested={}
        for _,r in self.rows_of(0x29): self.nested[r['NestedClass']]=r['EnclosingClass']
        self.propmap={}
        for _,r in self.rows_of(0x15): self.propmap[r['Parent']]=r['PropertyList']
        self.eventmap={}
        for _,r in self.rows_of(0x12): self.eventmap[r['Parent']]=r['EventList']
        self.sem={}
        for _,r in self.rows_of(0x18):
            self.sem.setdefault(r['Association'],[]).append((r['Semantics'],r['Method']))
        self.ca={}
        for _,r in self.rows_of(0x0C): self.ca.setdefault(r['Parent'],[]).append(r)
        self.ifaces={}
        for _,r in self.rows_of(0x09): self.ifaces.setdefault(r['Class'],[]).append(r['Interface'])
        self.typename={}
        for rid,r in self.rows_of(0x02): self.typename[rid]=self._tdname(rid,r)
        self.byname={v:k for k,v in self.typename.items()}

    def _tdname(self,rid,r=None):
        r=r or self.row(0x02,rid)
        n=r['Name']
        if rid in self.nested:
            return self._tdname(self.nested[rid])+'/'+n
        return (r['Namespace']+'.'+n) if r['Namespace'] else n

    def trname(self,rid):
        r=self.row(0x01,rid)
        if not r: return '?'
        n=r['Name']; ns=r['Namespace']
        scope=r['ResolutionScope']
        if scope[0]==0x01:
            return self.trname(scope[1])+'/'+n
        return (ns+'.'+n) if ns else n

    def tdr(self,cod):
        t,r=cod
        if t==0x02: return self.typename.get(r,'?')
        if t==0x01: return self.trname(r)
        if t==0x1B: 
            try: return self.sigtype(self.blob(self.row(0x1B,r)['Signature']),0)[0]
            except Exception: return 'TypeSpec#%d'%r
        return ''

    # --- signature decoding ---
    def sigtype(self,b,o):
        if o>=len(b): return '?',o
        e=b[o]; o+=1
        if e in ET: return ET[e],o
        if e in (0x11,0x12):
            v,n=self._cint(b,o); o+=n
            tag=v&3; rid=v>>2
            tt={0:0x02,1:0x01,2:0x1B}.get(tag,-1)
            return self.tdr((tt,rid)),o
        if e==0x0f:
            t,o=self.sigtype(b,o); return t+'*',o
        if e==0x10:
            t,o=self.sigtype(b,o); return t+'&',o
        if e==0x1d:
            t,o=self.sigtype(b,o); return t+'[]',o
        if e==0x45:
            return self.sigtype(b,o)
        if e in (0x1f,0x20):
            v,n=self._cint(b,o); o+=n
            return self.sigtype(b,o)
        if e==0x13:
            v,n=self._cint(b,o); o+=n; return '!%d'%v,o
        if e==0x1e:
            v,n=self._cint(b,o); o+=n; return '!!%d'%v,o
        if e==0x15:
            t,o=self.sigtype(b,o)
            cnt,n=self._cint(b,o); o+=n
            args=[]
            for _ in range(cnt):
                a,o=self.sigtype(b,o); args.append(a)
            return '%s<%s>'%(t.split('`')[0],', '.join(args)),o
        if e==0x14:
            t,o=self.sigtype(b,o)
            rank,n=self._cint(b,o); o+=n
            ns,n=self._cint(b,o); o+=n
            for _ in range(ns):
                _,n=self._cint(b,o); o+=n
            nl,n=self._cint(b,o); o+=n
            for _ in range(nl):
                _,n=self._cint(b,o); o+=n
            return '%s[%s]'%(t,','*(rank-1)),o
        if e==0x1b:
            cnt,n=self._cint(b,o); o+=n
            return 'fnptr',o
        return 'et_0x%02x'%e,o

    def methodsig(self,blob):
        b=blob
        if not b: return '?',[] 
        o=0; f=b[o]; o+=1
        gp=0
        if f&0x10:
            gp,n=self._cint(b,o); o+=n
        pc,n=self._cint(b,o); o+=n
        ret,o=self.sigtype(b,o)
        ps=[]
        for _ in range(pc):
            if o<len(b) and b[o]==0x41:  # SENTINEL
                o+=1
            t,o=self.sigtype(b,o); ps.append(t)
        return ret,ps

    def fieldsig(self,blob):
        if not blob: return '?'
        o=0
        if blob[o]==0x06: o+=1
        t,_=self.sigtype(blob,o); return t

    def propsig(self,blob):
        if not blob: return '?'
        o=0; f=blob[o]; o+=1
        pc,n=self._cint(blob,o); o+=n
        t,_=self.sigtype(blob,o); return t

    def ca_names(self,parent):
        out=[]
        for r in self.ca.get(parent,[]):
            t,rid=r['Type']
            if t==0x0A:
                mr=self.row(0x0A,rid)
                out.append(self.tdr(mr['Class']))
            elif t==0x06:
                out.append('?')
        return out

    @staticmethod
    def _serstring(b,o):
        """Decode a SerString at offset o -> (value, next_offset). None for null."""
        if o>=len(b): return None,o
        if b[o]==0xFF: return None,o+1
        n,k=Asm._cint(b,o); o+=k
        if o+n>len(b): return None,len(b)
        return b[o:o+n].decode('utf-8','replace'),o+n

    def ca_values(self,parent):
        """[(attrTypeName, firstStringArg|None)] — decodes the common single-string ctor case."""
        out=[]
        for r in self.ca.get(parent,[]):
            t,rid=r['Type']
            name='?'
            if t==0x0A:
                mr=self.row(0x0A,rid); name=self.tdr(mr['Class'])
            val=None
            try:
                blob=self.blob(r['Value'])
                if len(blob)>=2 and blob[0]==0x01 and blob[1]==0x00:
                    val,_=self._serstring(blob,2)
            except Exception:
                val=None
            out.append((name,val))
        return out

    def assembly_info(self):
        r=self.row(0x20,1)
        d={'name':r['Name'] if r else None,
           'version':'%d.%d.%d.%d'%(r['Major'],r['Minor'],r['Build'],r['Rev']) if r else None,
           'runtime':self.runtime_version,'attrs':{}}
        if r:
            for n,v in self.ca_values((0x20,1)):
                if v is not None:
                    d['attrs'][n.split('.')[-1]]=v
        return d

    # --- member enumeration ---
    def type_members(self,rid):
        td=self.row(0x02,rid)
        nx=self.row(0x02,rid+1)
        f0=td['FieldList']; f1=nx['FieldList'] if nx else self.nrows(0x04)+1
        m0=td['MethodList']; m1=nx['MethodList'] if nx else self.nrows(0x06)+1
        return (f0,f1),(m0,m1)

    def type_props(self,rid):
        if rid not in self.propmap: return (0,0)
        start=self.propmap[rid]
        ends=sorted([v for k,v in self.propmap.items() if v>start])
        end=ends[0] if ends else self.nrows(0x17)+1
        return (start,end)

    def type_events(self,rid):
        if rid not in self.eventmap: return (0,0)
        start=self.eventmap[rid]
        ends=sorted([v for k,v in self.eventmap.items() if v>start])
        end=ends[0] if ends else self.nrows(0x14)+1
        return (start,end)

TF_VIS=0x7; TF_IFACE=0x20; TF_ABSTRACT=0x80; TF_SEALED=0x100
MF_STATIC=0x10; MF_VIRTUAL=0x40; MF_ACC=0x7
FF_STATIC=0x10

def acc(f):
    return {0:'private',1:'famandassem',2:'assembly',3:'family',4:'famorassem',5:'private',6:'public'}.get(f&7,'?') if False else ['compilercontrolled','private','famandassem','assembly','family','famorassem','public'][f&7]

def type_kind(a,rid,td):
    if td['Flags']&TF_IFACE: return 'interface'
    ext=a.tdr(td['Extends']) if td['Extends'][1] else ''
    if ext=='System.Enum': return 'enum'
    if ext=='System.ValueType': return 'struct'
    if ext in ('System.MulticastDelegate','System.Delegate'): return 'delegate'
    return 'class'

def dump_type(a,rid,show_private=True):
    td=a.row(0x02,rid); fn=a.typename[rid]
    ext=a.tdr(td['Extends']) if td['Extends'][1] else None
    kind=type_kind(a,rid,td)
    out=[]
    ifs=[a.tdr(i) for i in a.ifaces.get(rid,[])]
    hdr='%s %s'%(kind,fn)
    if ext and ext not in('System.Object','System.ValueType','System.Enum'): hdr+=' : %s'%ext
    if ifs: hdr+=(' , ' if ext else ' : ')+', '.join(ifs)
    out.append(hdr)
    ats=a.ca_names((0x02,rid))
    if ats: out.append('  [attrs] '+', '.join(sorted(set(ats))))
    (f0,f1),(m0,m1)=a.type_members(rid)
    p0,p1=a.type_props(rid); e0,e1=a.type_events(rid)
    # collect accessor methods to mark
    accessors=set()
    for assoc,lst in a.sem.items():
        for s,m in lst: accessors.add(m)
    for fr in range(f0,f1):
        r=a.row(0x04,fr)
        if not r: continue
        fl=r['Flags']
        va=acc(fl)
        if not show_private and va in('private','compilercontrolled'): continue
        mods=('static ' if fl&FF_STATIC else '')
        at=a.ca_names((0x04,fr))
        sfx=('  ['+', '.join(sorted(set(at)))+']') if at else ''
        out.append('  FIELD   %-12s %s%s %s%s'%(va,mods,a.fieldsig(a.blob(r['Signature'])),r['Name'],sfx))
    for pr in range(p0,p1):
        r=a.row(0x17,pr)
        if not r: continue
        sem=a.sem.get((0x17,pr),[])
        gs=''.join(sorted(set('g' if s&0x2 else ('s' if s&0x1 else '') for s,_ in sem)))
        accs=[]
        for s,m in sem:
            mr=a.row(0x06,m)
            if mr: accs.append(('get' if s&0x2 else 'set' if s&0x1 else 'oth')+':'+acc(mr['Flags'])+('/static' if mr['Flags']&MF_STATIC else ''))
        at=a.ca_names((0x17,pr))
        sfx=('  ['+', '.join(sorted(set(at)))+']') if at else ''
        out.append('  PROP    %s %s {%s}%s'%(a.propsig(a.blob(r['Type'])),r['Name'],' '.join(accs),sfx))
    for er in range(e0,e1):
        r=a.row(0x14,er)
        if not r: continue
        at=a.ca_names((0x14,er))
        sfx=('  ['+', '.join(sorted(set(at)))+']') if at else ''
        out.append('  EVENT   %s %s%s'%(a.tdr(r['EventType']),r['Name'],sfx))
    for mr in range(m0,m1):
        r=a.row(0x06,mr)
        if not r: continue
        if mr in accessors: continue
        fl=r['Flags']; va=acc(fl)
        if not show_private and va in('private','compilercontrolled'): continue
        ret,ps=a.methodsig(a.blob(r['Signature']))
        mods=('static ' if fl&MF_STATIC else '')+('virtual ' if fl&MF_VIRTUAL else '')
        at=a.ca_names((0x06,mr))
        sfx=('  ['+', '.join(sorted(set(at)))+']') if at else ''
        out.append('  METHOD  %-12s %s%s %s(%s)%s'%(va,mods,ret,r['Name'],', '.join(ps),sfx))
    return '\n'.join(out)

def load(p): return Asm(p)

def main():
    cmd=sys.argv[1]
    if cmd=='types':
        a=load(sys.argv[2]); pat=re.compile(sys.argv[3],re.I) if len(sys.argv)>3 else None
        for rid,td in a.rows_of(0x02):
            fn=a.typename[rid]
            if fn=='<Module>': continue
            if pat and not pat.search(fn): continue
            print('%-10s %s'%(type_kind(a,rid,td),fn))
    elif cmd=='type':
        a=load(sys.argv[2])
        for nm in sys.argv[3:]:
            rid=a.byname.get(nm)
            if rid is None:
                cand=[k for k in a.byname if k.split('.')[-1]==nm or k.endswith('.'+nm)]
                if len(cand)>=1: rid=a.byname[cand[0]]
            if rid is None: print('NOT FOUND: '+nm); continue
            print(dump_type(a,rid)); print()
    elif cmd=='grep':
        pat=re.compile(sys.argv[2],re.I)
        for p in sys.argv[3:]:
            try: a=load(p)
            except Exception as e: continue
            an=os.path.basename(p)
            for rid,td in a.rows_of(0x02):
                fn=a.typename[rid]
                (f0,f1),(m0,m1)=a.type_members(rid)
                p0,p1=a.type_props(rid); e0,e1=a.type_events(rid)
                if pat.search(fn): print('%s TYPE   %s'%(an,fn))
                for x in range(m0,m1):
                    r=a.row(0x06,x)
                    if r and pat.search(r['Name']):
                        ret,ps=a.methodsig(a.blob(r['Signature']))
                        print('%s METHOD %s::%s(%s) -> %s [%s%s]'%(an,fn,r['Name'],', '.join(ps),ret,acc(r['Flags']),' static' if r['Flags']&MF_STATIC else ''))
                for x in range(p0,p1):
                    r=a.row(0x17,x)
                    if r and pat.search(r['Name']): print('%s PROP   %s::%s : %s'%(an,fn,r['Name'],a.propsig(a.blob(r['Type']))))
                for x in range(e0,e1):
                    r=a.row(0x14,x)
                    if r and pat.search(r['Name']): print('%s EVENT  %s::%s : %s'%(an,fn,r['Name'],a.tdr(r['EventType'])))
                for x in range(f0,f1):
                    r=a.row(0x04,x)
                    if r and pat.search(r['Name']): print('%s FIELD  %s::%s : %s [%s]'%(an,fn,r['Name'],a.fieldsig(a.blob(r['Signature'])),acc(r['Flags'])))
    elif cmd=='attrs':
        pat=re.compile(sys.argv[2],re.I)
        for p in sys.argv[3:]:
            a=load(p); an=os.path.basename(p)
            for (pt,prid),lst in sorted(a.ca.items()):
                names=a.ca_names((pt,prid))
                if not any(pat.search(n) for n in names): continue
                if pt==0x02: print('%s TYPE   %s  <- %s'%(an,a.typename.get(prid,'?'),','.join(names)))
                elif pt==0x04: 
                    r=a.row(0x04,prid); print('%s FIELD  %s  <- %s'%(r['Name'] if r else '?',','.join(names),an))
                elif pt==0x17:
                    r=a.row(0x17,prid); print('%s PROP   %s  <- %s'%(an,r['Name'] if r else '?',','.join(names)))
    elif cmd=='asminfo':
        for p in sys.argv[2:]:
            a=load(p)
            r=a.row(0x20,1)
            print('%-52s v%d.%d.%d.%d  runtime=%s  types=%d'%(os.path.basename(p),r['Major'],r['Minor'],r['Build'],r['Rev'],a.runtime_version,a.nrows(0x02)))
    elif cmd=='asmattrs':
        import json
        out=[]
        for p in sys.argv[2:]:
            try:
                a=load(p); info=a.assembly_info(); info['file']=os.path.basename(p); out.append(info)
            except Exception as e:
                out.append({'file':os.path.basename(p),'error':str(e)})
        print(json.dumps(out,indent=2))
    elif cmd=='refs':
        a=load(sys.argv[2])
        for rid,r in a.rows_of(0x23):
            print('%s v%d.%d.%d.%d'%(r['Name'],r['Major'],r['Minor'],r['Build'],r['Rev']))
    elif cmd=='surface':
        # emit normalized signature list for diffing
        a=load(sys.argv[2])
        for rid,td in a.rows_of(0x02):
            fn=a.typename[rid]
            if fn=='<Module>': continue
            vis=td['Flags']&TF_VIS
            print('T %s'%fn)
            (f0,f1),(m0,m1)=a.type_members(rid)
            p0,p1=a.type_props(rid); e0,e1=a.type_events(rid)
            for x in range(m0,m1):
                r=a.row(0x06,x)
                if not r: continue
                ret,ps=a.methodsig(a.blob(r['Signature']))
                print('M %s::%s(%s):%s'%(fn,r['Name'],','.join(ps),ret))
            for x in range(p0,p1):
                r=a.row(0x17,x)
                if r: print('P %s::%s:%s'%(fn,r['Name'],a.propsig(a.blob(r['Type']))))
            for x in range(e0,e1):
                r=a.row(0x14,x)
                if r: print('E %s::%s:%s'%(fn,r['Name'],a.tdr(r['EventType'])))
            for x in range(f0,f1):
                r=a.row(0x04,x)
                if r: print('F %s::%s:%s'%(fn,r['Name'],a.fieldsig(a.blob(r['Signature']))))
    else:
        print('usage: types|type|grep|attrs|asmattrs|asminfo|refs|surface')

if __name__=='__main__': main()
